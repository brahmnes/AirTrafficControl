using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Validation;

using AirTrafficControl.Interfaces;
using atc.utilities;

namespace atcsvc.Controllers
{
    [Route("api/[controller]")]
    public class FlightsController : Controller
    {
        private readonly ISubject<Airplane> airplaneStateEventAggregator_;
        private readonly AtcSvc atcSvc_;
        private readonly ILogger logger_;

        public FlightsController(ISubject<Airplane> airplaneStateEventAggregator, AtcSvc atcSvc, ILogger<FlightsController> logger): base()
        {
            Requires.NotNull(airplaneStateEventAggregator, nameof(airplaneStateEventAggregator));
            Requires.NotNull(atcSvc, nameof(atcSvc));
            Requires.NotNull(logger, nameof(logger));

            airplaneStateEventAggregator_ = airplaneStateEventAggregator;
            atcSvc_ = atcSvc;
            logger_ = logger;
        }

        // GET api/flights
        [HttpGet]
        public IActionResult GetAllFlights()
        {
            // This does not fully comply with the server-sent events spec 
            // https://html.spec.whatwg.org/multipage/server-sent-events.html#server-sent-events  https://www.html5rocks.com/en/tutorials/eventsource/basics/
            // but is good enough for testing
            // The AirplaneStatePublisher will do most of the error handling/logging
            return new PushStreamResult("text/event-stream", (stream, cToken) => {
                airplaneStateEventAggregator_.Subscribe(new AirplaneStatePublisher(stream, logger_), cToken);
                return cToken.WhenCanceled();
            });
        }

        // PUT api/flights
        [HttpPut]
        public async Task<IActionResult> NewFlightAsync([FromBody] FlightPlan flightPlan)
        {
            await atcSvc_.NewFlightAsync(flightPlan);
            return NoContent();
        }

        // GET api/flights/count
        [HttpGet("count")]
        public async Task<IActionResult> GetFlightCount() 
        {
            int? flightCount = await atcSvc_.GetFlightCountAsync();
            if (flightCount.HasValue) {
                return Json(flightCount.Value, Serialization.GetAtcSerializerSettings());
            }
            else {
                return StatusCode((int) HttpStatusCode.ServiceUnavailable);
            }  
        }

        // GET api/flights/callsigns
        [HttpGet("callsigns")]
        public async Task<IActionResult> GetFlyingAirplaneCallSigns()
        {
            IEnumerable<string> callSigns = await atcSvc_.GetFlyingAirplaneCallSigns();
            return Json(callSigns, Serialization.GetAtcSerializerSettings());
        }

        // GET api/flights/health
        [HttpGet("health")]
        public IActionResult CheckHealth()
        {
            HealthStatus status = atcSvc_.CheckHealth();
            if (status.Healthy) {
                return Ok();
            }
            else {
                return StatusCode((int) HttpStatusCode.InternalServerError, status);
            }
        }
    }
}
