using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Validation;

using AirTrafficControl.Interfaces;
using atcsvc.TableStorage;

namespace atcsvc
{
    public class AtcSvc: IDisposable
    {
        private delegate Task AirplaneController(Airplane airplane, IDictionary<string, AirplaneState> future);

        private enum TimePassageHandling : int
        {
            Completed = 0,
            InProgress = 1
        }
        private const int InvalidTime = -1;
        private readonly TimeSpan WorldTimerPeriod = TimeSpan.FromSeconds(5);

        private Timer worldTimer_;
        private int timePassageHandling_ = (int)TimePassageHandling.Completed;
        private FlyingAirplanesTable flyingAirplanesTable_;
        private WorldStateTable worldStateTable_;
        private readonly ISubject<Airplane> airplaneStateEventAggregator_;
        private readonly IDictionary<Type, AirplaneController> AirplaneControllers;

        public AtcSvc(IConfiguration configuration, ISubject<Airplane> airplaneStateEventAggregator)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(airplaneStateEventAggregator, nameof(airplaneStateEventAggregator));

            AirplaneControllers = new Dictionary<Type, AirplaneController>()
            {
                { typeof(TaxiingState), HandleAirplaneTaxiing },
                { typeof(DepartingState), HandleAirplaneDeparting },
                { typeof(HoldingState), HandleAirplaneHolding },
                { typeof(EnrouteState), HandleAirplaneEnroute },
                { typeof(ApproachState), HandleAirplaneApproaching },
                { typeof(LandedState), HandleAirplaneLanded }
            };

            flyingAirplanesTable_ = new FlyingAirplanesTable(configuration);
            worldStateTable_ = new WorldStateTable(configuration);

            worldTimer_?.Dispose();
            worldTimer_ = new Timer(OnTimePassed, null, TimeSpan.FromSeconds(1), WorldTimerPeriod);
        }

        public void Dispose()
        {
            worldTimer_?.Dispose();
            worldTimer_ = null;
        }

        private void OnTimePassed(object state)
        {
            Task.Run(async () => {
                if (Interlocked.CompareExchange(ref timePassageHandling_, (int)TimePassageHandling.InProgress, (int)TimePassageHandling.Completed) == (int)TimePassageHandling.InProgress)
                {
                    // Time passage handling took longer than expected, let bail out and wait for next timer tick.
                    return;
                }

                try
                {
                    var flyingAirplaneCallSigns = await flyingAirplanesTable_.GetFlyingAirplaneCallSignsAsync(CancellationToken.None);
                    if (!flyingAirplaneCallSigns.Any())
                    {
                        return; // Nothing to do
                    }

                    var worldState = await worldStateTable_.GetWorldStateAsync(CancellationToken.None);
                    worldState.CurrentTime++;
                    await worldStateTable_.SetWorldStateAsync(worldState, CancellationToken.None);

                    IEnumerable<Airplane> flyingAirplanes = await GetFlyingAirplanesAsync(flyingAirplaneCallSigns);
                    var airplanesByDepartureTime = flyingAirplanes.OrderBy(airplane => (airplane.AirplaneState is TaxiingState) ? int.MaxValue : airplane.DepartureTime);
                    var future = new Dictionary<string, Airplane>();


                    // TODO: query flying airplane states and instruct them as necessary
                    // TODO: make sure the clients inquring about airplane states get a consistent view

                }
                finally
                {
                    timePassageHandling_ = (int)TimePassageHandling.Completed;
                }
            });
        }

        private async Task HandleAirplaneLanded(Airplane airplane, IDictionary<string, AirplaneState> future)
        {
            // Just remove the airplane form the flying airplanes set
            string callSign = airplane.FlightPlan.CallSign;
            await flyingAirplanesTable_.DeleteFlyingAirplaneCallSignAsync(callSign, CancellationToken.None);

            // Update the projected airplane state to "Unknown Location" to ensure we do not attempt to send any notifications about it.
            future[airplane.FlightPlan.CallSign] = null;

            // TODO: log airplane completed flight from departure to destination, in time=currentTime - departure time
        }

        private Task HandleAirplaneApproaching(Airplane airplane, IDictionary<string, AirplaneState> future)
        {
            // We assume that every approach is successful, so just make a note that the airplane will be in the Landed state
            FlightPlan flightPlan = airplane.FlightPlan;
            Assumes.NotNull(flightPlan);
            future[flightPlan.CallSign] = new LandedState(flightPlan.Destination);
            return Task.FromResult(true);
        }

        private async Task HandleAirplaneEnroute(Airplane airplane, IDictionary<string, AirplaneState> future)
        {
            EnrouteState enrouteState = (EnrouteState) airplane.AirplaneState;
            FlightPlan flightPlan = airplane.FlightPlan;

            if (enrouteState.To == flightPlan.Destination)
            {
                // Any other airplanes cleared for landing at this airport?
                if (future.Values.OfType<ApproachState>().Any(state => state.Airport == flightPlan.Destination))
                {
                    future[flightPlan.CallSign] = new HoldingState(flightPlan.Destination);
                    await SendInstructionAsync(flightPlan.CallSign, new HoldInstruction(flightPlan.Destination)).ConfigureAwait(false);
                    // TODO: log $"ATC: Issued holding instruction for {flightPlan.CallSign} at {flightPlan.Destination.Displayname} because another airplane has been cleared for approach at the same airport",
                }
                else
                {
                    future[flightPlan.CallSign] = new ApproachState(flightPlan.Destination);
                    await SendInstructionAsync(flightPlan.CallSign, new ApproachClearance(flightPlan.Destination)).ConfigureAwait(false);
                    // TODO log $"ATC: Issued approach clearance for {flightPlan.CallSign} at {flightPlan.Destination.DisplayName}"
                }
            }
            else
            {
                Fix nextFix = flightPlan.GetNextFix(enrouteState.To);

                // Is another airplane destined to the same fix?
                if (future.Values.OfType<EnrouteState>().Any(state => state.To == nextFix))
                {
                    // Hold at the end of the current route leg
                    future[flightPlan.CallSign] = new HoldingState(enrouteState.To);
                    await SendInstructionAsync(flightPlan.CallSign, new HoldInstruction(enrouteState.To)).ConfigureAwait(false);
                    // TODO  log $"ATC: Issued holding instruction for {flightPlan.CallSign} at {enrouteState.To.DisplayName} because of traffic contention at {nextFix.DisplayName}"
                }
                else
                {
                    // Just let it proceed to next fix, no instruction necessary
                    future[flightPlan.CallSign] = new EnrouteState(enrouteState.To, nextFix);
                    // TODO: log "ATC: Airplane {flightPlan.CallSign} is flying from {enrouteState.From.DisplayName} to {enrouteState.To.DisplayName}, next fix {nextFix.DisplayName}"
                }
            }
        }

        private async Task HandleAirplaneHolding(IAirplane airplaneProxy, AirplaneActorState airplaneActorState, IDictionary<string, AirplaneState> projectedAirplaneStates)
        {
            HoldingState holdingState = (HoldingState)airplaneActorState.AirplaneState;
            FlightPlan flightPlan = airplaneActorState.FlightPlan;

            // Case 1: airplane holding at destination airport
            if (holdingState.Fix == flightPlan.Destination)
            {
                // Grant approach clearance if no other airplane is cleared for approach at the same airport.
                if (!projectedAirplaneStates.Values.OfType<ApproachState>().Any(state => state.Airport == flightPlan.Destination))
                {
                    projectedAirplaneStates[flightPlan.CallSign] = new ApproachState(flightPlan.Destination);
                    await airplaneProxy.ReceiveInstructionAsync(new ApproachClearance(flightPlan.Destination)).ConfigureAwait(false);
                    ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} has been cleared for approach at {1}", flightPlan.CallSign, flightPlan.Destination.DisplayName);
                }
                else
                {
                    projectedAirplaneStates[flightPlan.CallSign] = new HoldingState(flightPlan.Destination);
                    ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} should continue holding at {1} because of other traffic landing",
                        flightPlan.CallSign, flightPlan.Destination.DisplayName);
                }

                return;
            }

            // Case 2: holding at some point enroute
            Fix nextFix = flightPlan.GetNextFix(holdingState.Fix);

            if (projectedAirplaneStates.Values.OfType<EnrouteState>().Any(enrouteState => enrouteState.To == nextFix))
            {
                projectedAirplaneStates[flightPlan.CallSign] = holdingState;
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} should continue holding at {1} because of traffic contention at {2}. Assuming compliance with previous instruction, no new instructions issued.",
                    flightPlan.CallSign, holdingState.Fix.DisplayName, nextFix.DisplayName);
            }
            else
            {
                projectedAirplaneStates[flightPlan.CallSign] = new EnrouteState(holdingState.Fix, nextFix);
                // We always optmimistically give an enroute clearance all the way to the destination
                await airplaneProxy.ReceiveInstructionAsync(new EnrouteClearance(flightPlan.Destination, flightPlan.FlightPath));
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} should end holding at {1} and proceed to destination, next fix {2}. Issued new enroute clearance.",
                    flightPlan.CallSign, holdingState.Fix.DisplayName, nextFix.DisplayName);
            }
        }

        private async Task HandleAirplaneDeparting(IAirplane airplaneProxy, AirplaneActorState airplaneActorState, IDictionary<string, AirplaneState> projectedAirplaneStates)
        {
            DepartingState departingState = (DepartingState)airplaneActorState.AirplaneState;
            FlightPlan flightPlan = airplaneActorState.FlightPlan;

            Fix nextFix = flightPlan.GetNextFix(departingState.Airport);

            if (projectedAirplaneStates.Values.OfType<EnrouteState>().Any(enrouteState => enrouteState.To == nextFix))
            {
                projectedAirplaneStates[flightPlan.CallSign] = new HoldingState(departingState.Airport);
                await airplaneProxy.ReceiveInstructionAsync(new HoldInstruction(departingState.Airport)).ConfigureAwait(false);
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Issued holding instruction for {0} at {1} because of traffic contention at {2}",
                    flightPlan.CallSign, departingState.Airport.DisplayName, nextFix.DisplayName);
            }
            else
            {
                projectedAirplaneStates[flightPlan.CallSign] = new EnrouteState(departingState.Airport, nextFix);
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} completed departure from {1} and proceeds enroute to destination, next fix {2}",
                    flightPlan.CallSign, departingState.Airport.DisplayName, nextFix.DisplayName);
            }
        }

        private async Task HandleAirplaneTaxiing(IAirplane airplaneProxy, AirplaneActorState airplaneActorState, IDictionary<string, AirplaneState> projectedAirplaneStates)
        {
            TaxiingState taxiingState = (TaxiingState)airplaneActorState.AirplaneState;
            FlightPlan flightPlan = airplaneActorState.FlightPlan;

            if (projectedAirplaneStates.Values.OfType<DepartingState>().Any(state => state.Airport == flightPlan.DeparturePoint))
            {
                projectedAirplaneStates[flightPlan.CallSign] = taxiingState;
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} continue taxi at {1}, another airplane departing",
                    flightPlan.CallSign, flightPlan.DeparturePoint.DisplayName);
            }
            else
            {
                projectedAirplaneStates[flightPlan.CallSign] = new DepartingState(flightPlan.DeparturePoint);
                await airplaneProxy.ReceiveInstructionAsync(new TakeoffClearance(flightPlan.DeparturePoint)).ConfigureAwait(false);
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} received takeoff clearance at {1}",
                    flightPlan.CallSign, flightPlan.DeparturePoint);
            }
        }

        private async Task<IEnumerable<Airplane>> GetFlyingAirplanesAsync(IEnumerable<string> flyingAirplaneCallSigns)
        {
            Requires.NotNullEmptyOrNullElements(flyingAirplaneCallSigns, nameof(flyingAirplaneCallSigns));

            using (var client = GetAirplaneSvcClient())
            {
                Func<string, Task<Airplane>> getAirplaneState = async (string callSign) =>
                {
                    var response = await client.GetAsync($"/api/airplane/{callSign}");
                    var body = await response.Content.ReadAsStringAsync();
                    JsonSerializer serializer = JsonSerializer.Create(Serialization.GetAtcSerializerSettings());
                    // TODO: log errors, if any
                    return response.IsSuccessStatusCode ? serializer.Deserialize<Airplane>(new JsonTextReader(new StringReader(body))) : null;
                };

                var retval = (await Task.WhenAll(flyingAirplaneCallSigns.Select(callSign => getAirplaneState(callSign)))).Where(airplane => airplane.AirplaneState != null);
                return retval;
            }
        }

        private async Task SendInstructionAsync(string callSign, AtcInstruction instruction)
        {
            Requires.NotNullOrWhiteSpace(callSign, nameof(callSign));
            Requires.NotNull(instruction, nameof(instruction));

            using (var client = GetAirplaneSvcClient())
            using (var memoryStream = new MemoryStream())
            {
                JsonSerializer serializer = JsonSerializer.Create(Serialization.GetAtcSerializerSettings());

                var writer = new StreamWriter(memoryStream, Encoding.UTF8);
                serializer.Serialize(new JsonTextWriter(writer), instruction);
                writer.Flush();
                memoryStream.Position = 0;
                var body = new StreamContent(memoryStream);
                body.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                var response = await client.PutAsync($"/api/airplane/clearance/{callSign}", body);
                if (!response.IsSuccessStatusCode)
                {
                    var ex = new HttpRequestException($"Sending instruction to airplane {callSign} has failed");
                    ex.Data.Add("Instruction", instruction);
                    ex.Data.Add("HttpResponse", response);
                    throw ex;
                }
            }
        }

        private HttpClient GetAirplaneSvcClient()
        {
            var client = new HttpClient();
            string host = Environment.GetEnvironmentVariable("AIRPLANE_SERVICE_HOST");
            string port = Environment.GetEnvironmentVariable("AIRPLANE_SERVICE_PORT");
            // TODO: log errors if environment variables are not set
            client.BaseAddress = new Uri($"http://{host}:{port}");
            return client;
        }
    }
}
