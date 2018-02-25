using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Validation;

using AirTrafficControl.Interfaces;
using atcsvc.TableStorage;

namespace atcsvc
{
    public class Atc
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

        public Atc(ISubject<Airplane> airplaneStateEventAggregator)
        {
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


        }

        private async Task HandleAirplaneLanded(Airplane airplane, IDictionary<string, AirplaneState> future)
        {
            // Just remove the airplane form the flying airplanes set
                string airplaneID = airplane.FlightPlan.AirplaneID;

                var result = await this.flyingAirplaneIDs.TryRemoveAsync(tx, airplaneID);
                Debug.Assert(result.HasValue, $"Airplane {airplaneID} should be flying but we could not find it in the flying airplane dictionary");

                // Update the projected airplane state to "Unknown Location" to ensure we do not attempt to send any notifications about it.
                future[airplane.FlightPlan.AirplaneID] = null;

                int currentTime = await GetCurrentTime(tx);
            
            // TODO: log airplane completed flight from departure to destination, in time=currentTime - departure time
        }

        private Task HandleAirplaneApproaching(IAirplane airplaneProxy, AirplaneActorState airplaneActorState, IDictionary<string, AirplaneState> projectedAirplaneStates)
        {
            // We assume that every approach is successful, so just make a note that the airplane will be in the Landed state
            FlightPlan flightPlan = airplaneActorState.FlightPlan;
            Assumes.NotNull(flightPlan);
            projectedAirplaneStates[flightPlan.AirplaneID] = new LandedState(flightPlan.Destination);
            return Task.FromResult(true);
        }

        private async Task HandleAirplaneEnroute(IAirplane airplaneProxy, AirplaneActorState airplaneActorState, IDictionary<string, AirplaneState> projectedAirplaneStates)
        {
            EnrouteState enrouteState = (EnrouteState)airplaneActorState.AirplaneState;
            FlightPlan flightPlan = airplaneActorState.FlightPlan;

            if (enrouteState.To == flightPlan.Destination)
            {
                // Any other airplanes cleared for landing at this airport?
                if (projectedAirplaneStates.Values.OfType<ApproachState>().Any(state => state.Airport == flightPlan.Destination))
                {
                    projectedAirplaneStates[flightPlan.AirplaneID] = new HoldingState(flightPlan.Destination);
                    await airplaneProxy.ReceiveInstructionAsync(new HoldInstruction(flightPlan.Destination)).ConfigureAwait(false);
                    ServiceEventSource.Current.ServiceMessage(this, "ATC: Issued holding instruction for {0} at {1} because another airplane has been cleared for approach at the same airport",
                        flightPlan.AirplaneID, flightPlan.Destination.DisplayName);
                }
                else
                {
                    projectedAirplaneStates[flightPlan.AirplaneID] = new ApproachState(flightPlan.Destination);
                    await airplaneProxy.ReceiveInstructionAsync(new ApproachClearance(flightPlan.Destination)).ConfigureAwait(false);
                    ServiceEventSource.Current.ServiceMessage(this, "ATC: Issued approach clearance for {0} at {1}", flightPlan.AirplaneID, flightPlan.Destination.DisplayName);
                }
            }
            else
            {
                Fix nextFix = flightPlan.GetNextFix(enrouteState.To);

                // Is another airplane destined to the same fix?
                if (projectedAirplaneStates.Values.OfType<EnrouteState>().Any(state => state.To == nextFix))
                {
                    // Hold at the end of the current route leg
                    projectedAirplaneStates[flightPlan.AirplaneID] = new HoldingState(enrouteState.To);
                    await airplaneProxy.ReceiveInstructionAsync(new HoldInstruction(enrouteState.To)).ConfigureAwait(false);
                    ServiceEventSource.Current.ServiceMessage(this, "ATC: Issued holding instruction for {0} at {1} because of traffic contention at {2}",
                        flightPlan.AirplaneID, enrouteState.To.DisplayName, nextFix.DisplayName);
                }
                else
                {
                    // Just let it proceed to next fix, no instruction necessary
                    projectedAirplaneStates[flightPlan.AirplaneID] = new EnrouteState(enrouteState.To, nextFix);
                    ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} is flying from {1} to {2}, next fix {3}",
                        flightPlan.AirplaneID, enrouteState.From.DisplayName, enrouteState.To.DisplayName, nextFix.DisplayName);
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
                    projectedAirplaneStates[flightPlan.AirplaneID] = new ApproachState(flightPlan.Destination);
                    await airplaneProxy.ReceiveInstructionAsync(new ApproachClearance(flightPlan.Destination)).ConfigureAwait(false);
                    ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} has been cleared for approach at {1}", flightPlan.AirplaneID, flightPlan.Destination.DisplayName);
                }
                else
                {
                    projectedAirplaneStates[flightPlan.AirplaneID] = new HoldingState(flightPlan.Destination);
                    ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} should continue holding at {1} because of other traffic landing",
                        flightPlan.AirplaneID, flightPlan.Destination.DisplayName);
                }

                return;
            }

            // Case 2: holding at some point enroute
            Fix nextFix = flightPlan.GetNextFix(holdingState.Fix);

            if (projectedAirplaneStates.Values.OfType<EnrouteState>().Any(enrouteState => enrouteState.To == nextFix))
            {
                projectedAirplaneStates[flightPlan.AirplaneID] = holdingState;
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} should continue holding at {1} because of traffic contention at {2}. Assuming compliance with previous instruction, no new instructions issued.",
                    flightPlan.AirplaneID, holdingState.Fix.DisplayName, nextFix.DisplayName);
            }
            else
            {
                projectedAirplaneStates[flightPlan.AirplaneID] = new EnrouteState(holdingState.Fix, nextFix);
                // We always optmimistically give an enroute clearance all the way to the destination
                await airplaneProxy.ReceiveInstructionAsync(new EnrouteClearance(flightPlan.Destination, flightPlan.FlightPath));
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} should end holding at {1} and proceed to destination, next fix {2}. Issued new enroute clearance.",
                    flightPlan.AirplaneID, holdingState.Fix.DisplayName, nextFix.DisplayName);
            }
        }

        private async Task HandleAirplaneDeparting(IAirplane airplaneProxy, AirplaneActorState airplaneActorState, IDictionary<string, AirplaneState> projectedAirplaneStates)
        {
            DepartingState departingState = (DepartingState)airplaneActorState.AirplaneState;
            FlightPlan flightPlan = airplaneActorState.FlightPlan;

            Fix nextFix = flightPlan.GetNextFix(departingState.Airport);

            if (projectedAirplaneStates.Values.OfType<EnrouteState>().Any(enrouteState => enrouteState.To == nextFix))
            {
                projectedAirplaneStates[flightPlan.AirplaneID] = new HoldingState(departingState.Airport);
                await airplaneProxy.ReceiveInstructionAsync(new HoldInstruction(departingState.Airport)).ConfigureAwait(false);
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Issued holding instruction for {0} at {1} because of traffic contention at {2}",
                    flightPlan.AirplaneID, departingState.Airport.DisplayName, nextFix.DisplayName);
            }
            else
            {
                projectedAirplaneStates[flightPlan.AirplaneID] = new EnrouteState(departingState.Airport, nextFix);
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} completed departure from {1} and proceeds enroute to destination, next fix {2}",
                    flightPlan.AirplaneID, departingState.Airport.DisplayName, nextFix.DisplayName);
            }
        }

        private async Task HandleAirplaneTaxiing(IAirplane airplaneProxy, AirplaneActorState airplaneActorState, IDictionary<string, AirplaneState> projectedAirplaneStates)
        {
            TaxiingState taxiingState = (TaxiingState)airplaneActorState.AirplaneState;
            FlightPlan flightPlan = airplaneActorState.FlightPlan;

            if (projectedAirplaneStates.Values.OfType<DepartingState>().Any(state => state.Airport == flightPlan.DeparturePoint))
            {
                projectedAirplaneStates[flightPlan.AirplaneID] = taxiingState;
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} continue taxi at {1}, another airplane departing",
                    flightPlan.AirplaneID, flightPlan.DeparturePoint.DisplayName);
            }
            else
            {
                projectedAirplaneStates[flightPlan.AirplaneID] = new DepartingState(flightPlan.DeparturePoint);
                await airplaneProxy.ReceiveInstructionAsync(new TakeoffClearance(flightPlan.DeparturePoint)).ConfigureAwait(false);
                ServiceEventSource.Current.ServiceMessage(this, "ATC: Airplane {0} received takeoff clearance at {1}",
                    flightPlan.AirplaneID, flightPlan.DeparturePoint);
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
                    return response.IsSuccessStatusCode ? serializer.Deserialize<Airplane>(new JsonTextReader(new StringReader(body))) : null;
                };

                var retval = (await Task.WhenAll(flyingAirplaneCallSigns.Select(callSign => getAirplaneState(callSign)))).Where(state => state != null);
                return retval;
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
