using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Validation;
using Polly;
using AirTrafficControl.Interfaces;
using atcsvc.TableStorage;

namespace atcsvc
{
    public class AtcSvc
    {
        public class LoggingEvents
        {
            public const int TableStorageOpFailed = 1;
            public const int AirplaneSvcOpFailed = 2;
            public const int TimePassageHandlingFailed = 3;

            public const int NewFlightCreated = 1000;
            public const int FlightLanded = 1001;
            public const int InstructionIssued = 1002;

            public const string DefaultFailedOperationMessage = "{Operation} failed";
        }

        private delegate Task AirplaneController(Airplane airplane, IDictionary<string, AirplaneState> future);

        private enum TimePassageHandling : int
        {
            Completed = 0,
            InProgress = 1
        }
        private readonly TimeSpan WorldTimerPeriod = TimeSpan.FromSeconds(5);
        private readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

        private Timer worldTimer_;
        private int timePassageHandling_ = (int)TimePassageHandling.Completed;
        private FlyingAirplanesTable flyingAirplanesTable_;
        private WorldStateTable worldStateTable_;
        private WorldStateEntity worldState_;
        private bool firstRun_;
        private readonly ISubject<Airplane> airplaneStateEventAggregator_;
        private readonly IDictionary<Type, AirplaneController> AirplaneControllers;
        private readonly ConcurrentQueue<FlightPlan> newFlightQueue_;
        private readonly CancellationTokenSource shutdownTokenSource_;
        private readonly ILogger<AtcSvc> logger_;


        public AtcSvc(IConfiguration configuration, ISubject<Airplane> airplaneStateEventAggregator, ILogger<AtcSvc> logger)
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

            shutdownTokenSource_ = new CancellationTokenSource();
            airplaneStateEventAggregator_ = airplaneStateEventAggregator;
            flyingAirplanesTable_ = new FlyingAirplanesTable(configuration);
            worldStateTable_ = new WorldStateTable(configuration);
            firstRun_ = true;
            logger_ = logger;

            newFlightQueue_ = new ConcurrentQueue<FlightPlan>();

            worldTimer_ = new Timer(OnTimePassed, null, TimeSpan.FromSeconds(1), WorldTimerPeriod);
        }


        public Task AsyncDispose()
        {
            worldTimer_?.Dispose();
            worldTimer_ = null;
            shutdownTokenSource_.Cancel();
            SpinWait.SpinUntil(() => timePassageHandling_ == (int)TimePassageHandling.Completed, ShutdownTimeout);
            return TableStorageOperationAsync(
                () => flyingAirplanesTable_.DeleteAllFlyingAirplaneCallSignsAsync(CancellationToken.None),
                "Could not remove all flying airplane call signs during service shutdown",
                nameof(FlyingAirplanesTable.DeleteAllFlyingAirplaneCallSignsAsync));
        }

        public async Task StartNewFlight(FlightPlan flightPlan)
        {
            FlightPlan.Validate(flightPlan, includeFlightPath: false);

            var flyingAirplaneCallSigns = await TableStorageOperationAsync(
                () => flyingAirplanesTable_.GetFlyingAirplaneCallSignsAsync(shutdownTokenSource_.Token),
                null,
                nameof(FlyingAirplanesTable.GetFlyingAirplaneCallSignsAsync));
            if (flyingAirplaneCallSigns.Contains(flightPlan.CallSign, StringComparer.OrdinalIgnoreCase))
            {
                // In real life airplanes can have multiple flight plans filed, just for different times. But here we assume there can be only one flight plan per airplane
                throw new InvalidOperationException($"The airplane {flightPlan.CallSign} is already flying");
                // CONSIDER forcing execution of the new flight plan here, instead of throwing an error.
            }

            flightPlan.FlightPath = Dispatcher.ComputeFlightPath(flightPlan.DeparturePoint, flightPlan.Destination);

            // The actual creation of the flight will be queued and handled by the time passage routine to ensure that there are no races 
            // during new world state calculation
            newFlightQueue_.Enqueue(flightPlan);            

            if (logger_.IsEnabled(LogLevel.Information)) {
                flightPlan.AddUniverseInfo();
                logger_.LogInformation(
                    LoggingEvents.NewFlightCreated,
                    "New flight created from {DeparturePoint} to {Destination} for {CallSign}. Clearance is {FlightPath}",
                    flightPlan.DeparturePoint.Name,
                    flightPlan.Destination.Name,
                    flightPlan.CallSign,
                    JsonConvert.SerializeObject(flightPlan.FlightPath, Serialization.GetAtcSerializerSettings()));
            }
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
                    if (firstRun_)
                    {
                        firstRun_ = false;
                        await TableStorageOperationAsync(
                            () => flyingAirplanesTable_.DeleteAllFlyingAirplaneCallSignsAsync(shutdownTokenSource_.Token),
                            "Could not remove all flying airplane call signs during initial service run",
                            nameof(FlyingAirplanesTable.DeleteAllFlyingAirplaneCallSignsAsync));
                    }

                    // First take care of new flights, if any
                    while (newFlightQueue_.TryDequeue(out FlightPlan flightPlan))
                    {
                        await DoStartNewFlightAsync(flightPlan);
                    }

                    var flyingAirplaneCallSigns = await TableStorageOperationAsync(
                        () => flyingAirplanesTable_.GetFlyingAirplaneCallSignsAsync(shutdownTokenSource_.Token),
                        null,
                        nameof(FlyingAirplanesTable.GetFlyingAirplaneCallSignsAsync));
                    if (!flyingAirplaneCallSigns.Any())
                    {
                        return; // Nothing else to do
                    }

                    worldState_ = await UpdateWorldStateAsync();

                    IEnumerable<Airplane> flyingAirplanes = await GetFlyingAirplanesAsync(flyingAirplaneCallSigns);

                    // First take care of the airplanes that are flying the longest, so they do not run out of fuel.
                    // Taxiing airplanes are handled last (they can be kept safely on the ground indefinitely, if necessary).
                    var airplanesByDepartureTime = flyingAirplanes.OrderBy(airplane => (airplane.AirplaneState is TaxiingState) ? int.MaxValue : airplane.DepartureTime);
                    var future = new Dictionary<string, AirplaneState>();

                    // Instruct each airplane what to do
                    foreach (var airplane in airplanesByDepartureTime)
                    {
                        var controllerFunction = this.AirplaneControllers[airplane.AirplaneState.GetType()];
                        Assumes.NotNull(controllerFunction);

                        await controllerFunction(airplane, future);
                    }

                    // Now that each airplane knows what to do, perform one iteration of the simulation (airplanes are flying/taking off/landing)
                    await NotifyTimePassed(worldState_.CurrentTime);

                    // Notify anybody who is listening about new airplane states
                    var futureAirplanes = airplanesByDepartureTime.Where(airplane => future[airplane.FlightPlan.CallSign] != null).Select(airplane =>
                    {
                        var futureAirplane = new Airplane(future[airplane.FlightPlan.CallSign], airplane.FlightPlan);
                        futureAirplane.DepartureTime = airplane.DepartureTime;
                        return futureAirplane;
                    });
                    foreach (var futureAirplane in futureAirplanes)
                    {
                        airplaneStateEventAggregator_.OnNext(futureAirplane);
                    }

                }
                catch (Exception ex)
                {
                    logger_.LogError(LoggingEvents.TimePassageHandlingFailed, ex, "Unexpected error occurred while handling time passage");
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
            await TableStorageOperationAsync(
                () => flyingAirplanesTable_.DeleteFlyingAirplaneCallSignAsync(callSign, shutdownTokenSource_.Token),
                null,
                nameof(FlyingAirplanesTable.DeleteFlyingAirplaneCallSignAsync));

            // Update the projected airplane state to "Unknown Location" to ensure we do not attempt to send any notifications about it.
            future[callSign] = null;

            Assumes.NotNull(worldState_);
            airplane.FlightPlan.AddUniverseInfo();
            logger_.LogInformation(LoggingEvents.FlightLanded, null,
                "{CallSign} completed flight from {DeparturePoint} to {Destination} in {Duration} time intervals",
                callSign, 
                airplane.FlightPlan.DeparturePoint.Name, 
                airplane.FlightPlan.Destination.Name,
                worldState_.CurrentTime - airplane.DepartureTime);
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
                    await SendInstructionAsync(flightPlan.CallSign, new HoldInstruction(flightPlan.Destination));

                    if (logger_.IsEnabled(LogLevel.Debug)) {
                        flightPlan.AddUniverseInfo();
                        logger_.LogInformation(LoggingEvents.InstructionIssued, null, 
                        "ATC: Issued holding instruction for {CallSign} at {Destination} because another airplane has been cleared for approach at the same airport",
                        flightPlan.CallSign,
                        flightPlan.Destination.Name);
                    }
                }
                else
                {
                    future[flightPlan.CallSign] = new ApproachState(flightPlan.Destination);
                    await SendInstructionAsync(flightPlan.CallSign, new ApproachClearance(flightPlan.Destination)).ConfigureAwait(false);

                    if (logger_.IsEnabled(LogLevel.Debug)) {
                        flightPlan.AddUniverseInfo();
                        logger_.LogInformation(LoggingEvents.InstructionIssued, null, 
                        "ATC: Issued approach clearance for {CallSign} at {Destination}",
                        flightPlan.CallSign,
                        flightPlan.Destination.Name);
                    }
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

        private async Task HandleAirplaneHolding(Airplane airplane, IDictionary<string, AirplaneState> future)
        {
            HoldingState holdingState = (HoldingState) airplane.AirplaneState;
            FlightPlan flightPlan = airplane.FlightPlan;

            // Case 1: airplane holding at destination airport
            if (holdingState.Fix == flightPlan.Destination)
            {
                // Grant approach clearance if no other airplane is cleared for approach at the same airport.
                if (!future.Values.OfType<ApproachState>().Any(state => state.Airport == flightPlan.Destination))
                {
                    future[flightPlan.CallSign] = new ApproachState(flightPlan.Destination);
                    await SendInstructionAsync(flightPlan.CallSign, new ApproachClearance(flightPlan.Destination)).ConfigureAwait(false);
                    // TODO log $"ATC: Airplane {flightPlan.CallSign} has been cleared for approach at {flightPlan.Destination.DisplayName}"
                }
                else
                {
                    future[flightPlan.CallSign] = new HoldingState(flightPlan.Destination);
                    // TODO log $"ATC: Airplane {flightPlan.CallSign} should continue holding at {flightPlan.Destination.DisplayName} because of other traffic landing"
                }

                return;
            }

            // Case 2: holding at some point enroute
            Fix nextFix = flightPlan.GetNextFix(holdingState.Fix);

            if (future.Values.OfType<EnrouteState>().Any(enrouteState => enrouteState.To == nextFix))
            {
                future[flightPlan.CallSign] = holdingState;
                // TODO log "ATC: Airplane {0} should continue holding at {1} because of traffic contention at {2}. Assuming compliance with previous instruction, no new instructions issued.",
                //    flightPlan.CallSign, holdingState.Fix.DisplayName, nextFix.DisplayName);
            }
            else
            {
                future[flightPlan.CallSign] = new EnrouteState(holdingState.Fix, nextFix);
                // We always optmimistically give an enroute clearance all the way to the destination
                await SendInstructionAsync(flightPlan.CallSign, new EnrouteClearance(flightPlan.Destination, flightPlan.FlightPath));
                // TODO log "ATC: Airplane {0} should end holding at {1} and proceed to destination, next fix {2}. Issued new enroute clearance.",
                //    flightPlan.CallSign, holdingState.Fix.DisplayName, nextFix.DisplayName);
            }
        }

        private async Task HandleAirplaneDeparting(Airplane airplane, IDictionary<string, AirplaneState> future)
        {
            DepartingState departingState = (DepartingState)airplane.AirplaneState;
            FlightPlan flightPlan = airplane.FlightPlan;

            Fix nextFix = flightPlan.GetNextFix(departingState.Airport);

            if (future.Values.OfType<EnrouteState>().Any(enrouteState => enrouteState.To == nextFix))
            {
                future[flightPlan.CallSign] = new HoldingState(departingState.Airport);
                await SendInstructionAsync(flightPlan.CallSign, new HoldInstruction(departingState.Airport)).ConfigureAwait(false);
                // TODO log "ATC: Issued holding instruction for {0} at {1} because of traffic contention at {2}",
                //    flightPlan.CallSign, departingState.Airport.DisplayName, nextFix.DisplayName);
            }
            else
            {
                future[flightPlan.CallSign] = new EnrouteState(departingState.Airport, nextFix);
                // TODO log "ATC: Airplane {0} completed departure from {1} and proceeds enroute to destination, next fix {2}",
                //    flightPlan.CallSign, departingState.Airport.DisplayName, nextFix.DisplayName);
            }
        }

        private async Task HandleAirplaneTaxiing(Airplane airplane, IDictionary<string, AirplaneState> future)
        {
            TaxiingState taxiingState = (TaxiingState) airplane.AirplaneState;
            FlightPlan flightPlan = airplane.FlightPlan;

            if (future.Values.OfType<DepartingState>().Any(state => state.Airport == flightPlan.DeparturePoint))
            {
                future[flightPlan.CallSign] = taxiingState;
                // TODO log "ATC: Airplane {0} continue taxi at {1}, another airplane departing", flightPlan.CallSign, flightPlan.DeparturePoint.DisplayName);
            }
            else
            {
                future[flightPlan.CallSign] = new DepartingState(flightPlan.DeparturePoint);
                await SendInstructionAsync(flightPlan.CallSign, new TakeoffClearance(flightPlan.DeparturePoint)).ConfigureAwait(false);
                // TODO log "ATC: Airplane {0} received takeoff clearance at {1}", flightPlan.CallSign, flightPlan.DeparturePoint);
            }
        }

        private async Task DoStartNewFlightAsync(FlightPlan flightPlan)
        {
            await StartNewFlightAsync(flightPlan);
            await TableStorageOperationAsync(
                () => flyingAirplanesTable_.AddFlyingAirplaneCallSignAsync(flightPlan.CallSign, shutdownTokenSource_.Token),
                "{Operation} failed: could not add new call sign {CallSign} to the list of flying airplanes",
                nameof(FlyingAirplanesTable.AddFlyingAirplaneCallSignAsync),
                flightPlan.CallSign);
        }

        private async Task<WorldStateEntity> UpdateWorldStateAsync()
        {
            var worldState = await TableStorageOperationAsync(
                () => worldStateTable_.GetWorldStateAsync(shutdownTokenSource_.Token),
                null,
                nameof(WorldStateTable.GetWorldStateAsync));
            worldState.CurrentTime++;
            await TableStorageOperationAsync(
                () => worldStateTable_.SetWorldStateAsync(worldState, shutdownTokenSource_.Token),
                null,
                nameof(WorldStateTable.SetWorldStateAsync));
            return worldState;
        }

        private Task<IEnumerable<Airplane>> GetFlyingAirplanesAsync(IEnumerable<string> flyingAirplaneCallSigns)
        {
            Requires.NotNullEmptyOrNullElements(flyingAirplaneCallSigns, nameof(flyingAirplaneCallSigns));

            return AirplaneSvcOperationAsync(async () => {
                using (var client = GetAirplaneSvcClient()) {
                    Func<string, Task<Airplane>> getAirplaneState = async (string callSign) => {
                        var response = await client.GetAsync($"{callSign}", shutdownTokenSource_.Token);
                        var body = await response.Content.ReadAsStringAsync();
                        JsonSerializer serializer = JsonSerializer.Create(Serialization.GetAtcSerializerSettings());
                        // TODO: log errors, if any
                        return response.IsSuccessStatusCode ? serializer.Deserialize<Airplane>(new JsonTextReader(new StringReader(body))) : null;
                    };

                    var retval = (await Task.WhenAll(flyingAirplaneCallSigns.Select(callSign => getAirplaneState(callSign)))).Where(airplane => airplane.AirplaneState != null);
                    return retval;
                }
            }, "Could not get data about flying airplanes", nameof(GetFlyingAirplanesAsync));
        }

        private async Task SendInstructionAsync(string callSign, AtcInstruction instruction) {
            Requires.NotNullOrWhiteSpace(callSign, nameof(callSign));
            Requires.NotNull(instruction, nameof(instruction));

            await AirplaneSvcOperationAsync(async () => {
                using (var client = GetAirplaneSvcClient())
                using (var instructionJson = GetJsonContent(instruction)) {
                    var response = await client.PutAsync($"clearance/{callSign}", instructionJson, shutdownTokenSource_.Token);
                    if (!response.IsSuccessStatusCode) {
                        var ex = new HttpRequestException($"Sending instruction to airplane {callSign} has failed");
                        ex.Data.Add("Instruction", instruction);
                        ex.Data.Add("HttpResponse", response);
                        throw ex;
                    }
                }
            }, null, nameof(SendInstructionAsync));
        }

        private Task StartNewFlightAsync(FlightPlan flightPlan) {
            Requires.NotNull(flightPlan, nameof(flightPlan));

            return AirplaneSvcOperationAsync(async () => {
                using (var client = GetAirplaneSvcClient())
                using (var flightPlanJson = GetJsonContent(flightPlan)) {
                    var response = await client.PutAsync("newflight", flightPlanJson, shutdownTokenSource_.Token);
                    if (!response.IsSuccessStatusCode) {
                        var ex = new HttpRequestException("Could not start a new flight");
                        ex.Data.Add("FlightPlan", flightPlan);
                        throw ex;
                    }
                }
            }, "New flight could not be started", nameof(StartNewFlightAsync));
        }

        private Task NotifyTimePassed(int currentTime)
        {
            return AirplaneSvcOperationAsync(async () => {
                using (var client = GetAirplaneSvcClient()) {
                    var response = await client.PostAsync($"time/{currentTime}", null, shutdownTokenSource_.Token);
                    if (!response.IsSuccessStatusCode) {
                        throw new HttpRequestException($"Notifiying airplane service about new time failed. The current time is {currentTime}");
                    }
                }
            }, null, nameof(NotifyTimePassed));
        }

        private HttpClient GetAirplaneSvcClient()
        {
            var client = new HttpClient();
            string host = Environment.GetEnvironmentVariable("AIRPLANE_SERVICE_HOST");
            string port = Environment.GetEnvironmentVariable("AIRPLANE_SERVICE_PORT");
            // TODO: log errors if environment variables are not set

            // The somewhat well-known weirdness of HttpClient is that the BaseAddress MUST end with a slash
            // but relative path in the Get(), Post() etc. calls MUST NOT begin with a slash. Only this combo works.
            client.BaseAddress = new Uri($"http://{host}:{port}/api/airplane/");
            return client;
        }

        private StringContent GetJsonContent(object value)
        {
            Debug.Assert(value != null);

            var stringContent = new StringContent(
                JsonConvert.SerializeObject(value, Serialization.GetAtcSerializerSettings()), 
                Encoding.UTF8, 
                "application/json");
            return stringContent;
        }

        private Task TableStorageOperationAsync(Func<Task> operation, string errorMessage, params object[] args)
        {
            return NetworkErrorHandlingPolicy.ExecuteAsync(operation, (Exception ex, TimeSpan waitDuration) => {
                logger_.LogWarning(LoggingEvents.TableStorageOpFailed, ex, FailedNetworkOperationErrorMessage(errorMessage), args);
                return Task.CompletedTask;
            });
        }

        private Task<T> TableStorageOperationAsync<T>(Func<Task<T>> operation, string errorMessage, params object[] args)
        {
            return NetworkErrorHandlingPolicy.ExecuteAsync(operation, (Exception ex, TimeSpan waitDuration) => {
                logger_.LogWarning(LoggingEvents.TableStorageOpFailed, ex, FailedNetworkOperationErrorMessage(errorMessage), args);
                return Task.CompletedTask;
            });
        }

        private Task AirplaneSvcOperationAsync(Func<Task> operation, string errorMessage, params object[] args) 
        {
            return NetworkErrorHandlingPolicy.ExecuteAsync(operation, (Exception ex, TimeSpan waitDuration) => {
                logger_.LogWarning(LoggingEvents.AirplaneSvcOpFailed, ex, FailedNetworkOperationErrorMessage(errorMessage), args);
                return Task.CompletedTask;
            });
        }

        private Task<T> AirplaneSvcOperationAsync<T>(Func<Task<T>> operation, string errorMessage, params object[] args)
        {
            return NetworkErrorHandlingPolicy.ExecuteAsync(operation, (Exception ex, TimeSpan waitDuration) => {
                logger_.LogWarning(LoggingEvents.AirplaneSvcOpFailed, ex, FailedNetworkOperationErrorMessage(errorMessage), args);
                return Task.CompletedTask;
            });
        }
                
        private string FailedNetworkOperationErrorMessage(string userMessage) {
            var errorMessage = userMessage == null ?
                LoggingEvents.DefaultFailedOperationMessage :
                LoggingEvents.DefaultFailedOperationMessage + " " + userMessage;
            return errorMessage;
        }
    }
}
