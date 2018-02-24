using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Validation;

using AirTrafficControl.Interfaces;

namespace atcsvc.Controllers
{
    [Route("api/[controller]")]
    public class FlightsController : Controller
    {
        private readonly IEventAggregator<AirplaneStateDto> airplaneStateEventAggregator_;

        public FlightsController(IEventAggregator<AirplaneStateDto> airplaneStateEventAggregator): base()
        {
            Requires.NotNull(airplaneStateEventAggregator, nameof(airplaneStateEventAggregator));

            airplaneStateEventAggregator_ = airplaneStateEventAggregator;
        }

        // GET api/flights
        [HttpGet]
        public IActionResult Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
