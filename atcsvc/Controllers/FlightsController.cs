using System;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Validation;

using AirTrafficControl.Interfaces;
using System.Text;

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
        public async Task<IActionResult> StartNewFlightAsync([FromBody] FlightPlan flightPlan)
        {
            await atcSvc_.StartNewFlight(flightPlan);
            return NoContent();
        }
    }
}
