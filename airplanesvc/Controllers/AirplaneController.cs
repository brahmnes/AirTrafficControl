using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Validation;

using AirTrafficControl.Interfaces;

namespace airplanesvc.Controllers
{
    [Route("api/[controller]")]
    public class AirplaneController : Controller
    {
        private AirplaneRepository airplaneRepository_;

        public AirplaneController(AirplaneRepository airplaneRepository)
        {
            Requires.NotNull(airplaneRepository, nameof(airplaneRepository));

            airplaneRepository_ = airplaneRepository;
        }

        // GET api/airplane/N2130U
        [HttpGet]
        public IActionResult Get(string callSign)
        {
            Requires.NotNullOrWhiteSpace(callSign, nameof(callSign));

            var airplane = EnsureAirplane(callSign);

            return Json(airplane, Serialization.GetAtcSerializerSettings());
        }

        [HttpPut("clearance/{callSign}")]
        public IActionResult ReceiveInstruction(string callSign, [FromBody]AtcInstruction instruction)
        {
            Requires.NotNull(instruction, "instruction");
            Requires.NotNullOrWhiteSpace(callSign, nameof(callSign));

            var airplane = EnsureAirplane(callSign);
            lock (airplane)
            {
                if (airplane.AirplaneState == null)
                {
                    throw new InvalidOperationException("Cannot receive ATC instruction if the airplane location is unknown. The airplane needs to start the flight first.");
                }

                airplane.Instruction = instruction;
            }

            return NoContent();
        }

        [HttpPut("newflight")]
        public IActionResult StartFlight([FromBody]FlightPlan fligthPlan)
        {
            if (fligthPlan == null) {
                return BadRequest("Flight plan for new flight is invalid or missing");
            }

            try {
                FlightPlan.Validate(fligthPlan, includeFlightPath: true);
            } 
            catch (Exception e) {
                return BadRequest(e);
            }

            var airplane = EnsureAirplane(fligthPlan.CallSign);
            lock (airplane) {
                if (airplane.AirplaneState != null) {
                    return BadRequest($"Airplane {fligthPlan.CallSign} is currently flying");
                }
                airplane.AirplaneState = new TaxiingState(fligthPlan.DeparturePoint);
                airplane.FlightPlan = fligthPlan;
            }

            return NoContent();
        }

        [HttpPost("time/{currentTime}")]
        public IActionResult TimePassed(int currentTime)
        {
            foreach(var entry in airplaneRepository_) {
                var airplane = entry.Value;

                lock(airplane) {
                    if (airplane.AirplaneState == null) {
                        continue;
                    }

                    var newState = airplane.AirplaneState.ComputeNextState(airplane.FlightPlan, airplane.Instruction);
                    airplane.AirplaneState = newState;

                    if (newState is DepartingState) {
                        airplane.DepartureTime = currentTime;
                    }

                    if (newState == null) {
                        // The airplane is done flying; clear the rest of the state
                        airplane.DepartureTime = 0;
                        airplane.FlightPlan = null;
                        airplane.Instruction = null;
                    }
                }
            }

            return NoContent();
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
