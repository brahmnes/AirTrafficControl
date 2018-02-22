using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using AirTrafficControl.Interfaces;

namespace airplanesvc.Controllers
{
    [Route("api/[controller]")]
    public class AirplaneController : Controller
    {
        // GET api/airplane/N2130U
        [HttpGet]
        public IActionResult Get(string callSign)
        {
            // TODO: implement and return AirplaneStateDto
            return Json(null);
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
            // TODO: implement
            return NoContent();
        }

        [HttpPost("time/{currentTime}")]
        public IActionResult TimePassed(int currentTime)
        {
            // TODO: implement
            return NoContent();
        }
    }
}
