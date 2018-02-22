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
            if (string.IsNullOrEmpty(callSign)) {
                return BadRequest("Must provide valid airplane call sign");    
            }

            var airplane = EnsureAirplane(callSign);

            var retval = new AirplaneStateDto(airplane.State, airplane.FlightPlan);
            return Json(retval);
        }

        [HttpPut("clearance/{callSign}")]
        public IActionResult ReceiveInstruction(string callSign, [FromBody]AtcInstruction instruction)
        {
            // TODO: implement
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
            catch (Exception e){
                return BadRequest(e);
            }

            var airplane = EnsureAirplane(fligthPlan.AirplaneID);
            airplane.State = new TaxiingState(fligthPlan.DeparturePoint);
            airplane.FlightPlan = fligthPlan;
            return NoContent();
        }

        [HttpPost("time/{currentTime}")]
        public IActionResult TimePassed(int currentTime)
        {
            // TODO: implement
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
