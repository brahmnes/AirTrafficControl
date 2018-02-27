using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Validation;

using AirTrafficControl.Interfaces;

namespace airplanesvc.Controllers
{
    [Route("api/[controller]")]
    public class AirplaneController : Controller
    {
        private class LoggingEvents
        {
            public const int InstructionProcessingFailed = 1;
            public const int StartingNewFlightFailed = 2;
            public const int TrafficSimulationFailed = 3;

            public const int InstructionReceived = 1000;
            public const int NewFlightStarted = 1001;
            public const int TrafficSimulationCompleted = 1002;
        }

        private AirplaneRepository airplaneRepository_;
        private ILogger<AirplaneController> logger_;

        public AirplaneController(AirplaneRepository airplaneRepository, ILogger<AirplaneController> logger)
        {
            Requires.NotNull(airplaneRepository, nameof(airplaneRepository));
            Requires.NotNull(logger, nameof(logger));

            airplaneRepository_ = airplaneRepository;
            logger_ = logger;
        }

        // GET api/airplane/N2130U
        [HttpGet("{callSign}")]
        public IActionResult Get(string callSign)
        {
            if (string.IsNullOrWhiteSpace(callSign))
            {
                return BadRequest("Get request must receive a valid (non-empty) airplane call sign");
            }

            var airplane = EnsureAirplane(callSign);

            return Json(airplane, Serialization.GetAtcSerializerSettings());
        }

        [HttpPut("clearance/{callSign}")]
        public IActionResult ReceiveInstruction(string callSign, [FromBody]AtcInstruction instruction)
        {
            Requires.NotNull(instruction, "instruction");
            Requires.NotNullOrWhiteSpace(callSign, nameof(callSign));

            try
            {
                var airplane = EnsureAirplane(callSign);
                lock (airplane)
                {
                    if (airplane.AirplaneState == null)
                    {
                        throw new InvalidOperationException("Cannot receive ATC instruction if the airplane location is unknown. The airplane needs to start the flight first.");
                    }

                    airplane.Instruction = instruction;
                }

                if (logger_.IsEnabled(LogLevel.Debug))
                {
                    logger_.LogDebug(LoggingEvents.InstructionReceived, "Airplane {CallSign} received {Instruction}", callSign, instruction.ToString());
                }

                return NoContent();
            }
            catch(Exception e)
            {
                logger_.LogWarning(LoggingEvents.InstructionProcessingFailed, e, "Unexpected error ocurred when processing ATC instruction");
                throw;
            }
        }

        [HttpPut("newflight")]
        public IActionResult StartFlight([FromBody] FlightPlan flightPlan)
        {
            if (flightPlan == null) {
                return BadRequest("Flight plan for new flight is invalid or missing");
            }

            try {
                FlightPlan.Validate(flightPlan, includeFlightPath: true);
            } 
            catch (Exception e) {
                return BadRequest(e);
            }

            try
            {
                var airplane = EnsureAirplane(flightPlan.CallSign);
                lock (airplane)
                {
                    if (airplane.AirplaneState != null)
                    {
                        return BadRequest($"Airplane {flightPlan.CallSign} is currently flying");
                    }
                    airplane.AirplaneState = new TaxiingState(flightPlan.DeparturePoint);
                    airplane.FlightPlan = flightPlan;
                }

                if (logger_.IsEnabled(LogLevel.Debug))
                {
                    logger_.LogDebug(LoggingEvents.NewFlightStarted, "Airplane {CallSign} is departing from {DeparturePoint} to {Destination}. Flight path is {FlightPath}", 
                        flightPlan.CallSign, flightPlan.DeparturePoint.Name, flightPlan.Destination.Name,
                        flightPlan.FlightPath.Select(fix => fix.Name).Aggregate(string.Empty, (current, fixName) => current + " " + fixName));
                }

                return NoContent();
            }
            catch(Exception e)
            {
                logger_.LogWarning(LoggingEvents.StartingNewFlightFailed, e, "Starting new flight failed");
                throw;
            }
        }

        [HttpPost("time/{currentTime}")]
        public IActionResult TimePassed(int currentTime)
        {
            try
            {
                foreach (var entry in airplaneRepository_)
                {
                    var airplane = entry.Value;

                    lock (airplane)
                    {
                        if (airplane.AirplaneState == null)
                        {
                            continue;
                        }

                        var newState = airplane.AirplaneState.ComputeNextState(airplane.FlightPlan, airplane.Instruction);
                        airplane.AirplaneState = newState;

                        if (newState is DepartingState)
                        {
                            airplane.DepartureTime = currentTime;
                        }

                        if (newState == null)
                        {
                            // The airplane is done flying; clear the rest of the state
                            airplane.DepartureTime = 0;
                            airplane.FlightPlan = null;
                            airplane.Instruction = null;
                        }
                    }
                }

                if (logger_.IsEnabled(LogLevel.Debug))
                {
                    logger_.LogDebug(LoggingEvents.TrafficSimulationCompleted, 
                        "Traffic simulation completed, time is {CurrentTime}, {AirplaneCount} airplanes flying",
                        currentTime, airplaneRepository_.Values.Count(a => a.AirplaneState != null));
                }

                return NoContent();
            }
            catch(Exception e)
            {
                logger_.LogWarning(LoggingEvents.TrafficSimulationFailed, e, "Unexpected error occurred when simulating traffic");
                throw;
            }
        }

        private Airplane EnsureAirplane(string callSign)
        {
            if (!airplaneRepository_.TryGetValue(callSign, out Airplane airplane))
            {
                airplane = new Airplane();
                airplaneRepository_.AddOrUpdate(callSign, airplane, (existingCallSign, existingAirplane) => existingAirplane);
            }
            return airplane;
        }
    }
}
