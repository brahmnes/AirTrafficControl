using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Validation;

using AirTrafficControl.Interfaces;

namespace atcsvc
{
    internal class AirplaneStatePublisher : IObserver<Airplane>
    {
        private readonly Stream stream_;
        private readonly JsonTextWriter writer_;
        private readonly ILogger logger_;

        public AirplaneStatePublisher(Stream stream, ILogger logger)
        {
            Requires.NotNull(stream, nameof(stream));
            Requires.NotNull(logger, nameof(logger));

            stream_ = stream;
            writer_ = new JsonTextWriter(new StreamWriter(stream_, Encoding.UTF8));
            logger_ = logger;
        }

        public void OnCompleted()
        {
            stream_.Dispose();
        }

        public void OnError(Exception error)
        {
            logger_.LogWarning(LoggingEvents.StreamingAirplaneInformationFailed, error, "The source of airplane information reported unexpected error");
            stream_.Dispose();
        }

        public void OnNext(Airplane airplane)
        {
            Task.Run(async () => {
                try
                {
                    writer_.WriteStartObject();

                    writer_.WritePropertyName("CallSign");
                    writer_.WriteValue(airplane.FlightPlan.CallSign);

                    writer_.WritePropertyName("State");
                    writer_.WriteValue(airplane.StateDescription);

                    writer_.WriteEndObject();

                    writer_.WriteWhitespace("\n");

                    // Ensure that the value is written out immediately to the network stream
                    // (the writer will flush the underlying stream too)
                    await writer_.FlushAsync();
                }
                catch (Exception e)
                {
                    logger_.LogWarning(LoggingEvents.StreamingAirplaneInformationFailed, e, "Writing airplane information to network stream failed");
                    throw;
                }
            });
            
        }
    }
}
