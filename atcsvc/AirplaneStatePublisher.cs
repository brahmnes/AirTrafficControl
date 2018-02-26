using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Validation;

using AirTrafficControl.Interfaces;

namespace atcsvc
{
    internal class AirplaneStatePublisher : IObserver<Airplane>
    {
        private readonly Stream stream_;
        private readonly JsonTextWriter writer_;
        private readonly JsonSerializer serializer_;

        public AirplaneStatePublisher(Stream stream)
        {
            Requires.NotNull(stream, nameof(stream));

            stream_ = stream;
            writer_ = new JsonTextWriter(new StreamWriter(stream_, Encoding.UTF8));
        }

        public void OnCompleted()
        {
            stream_.Dispose();
        }

        public void OnError(Exception error)
        {
            // TODO: log error
            stream_.Dispose();
        }

        public void OnNext(Airplane airplane)
        {
            writer_.WriteStartObject();

            writer_.WritePropertyName("CallSign");
            writer_.WriteValue(airplane.FlightPlan.CallSign);

            writer_.WritePropertyName("State");
            writer_.WriteValue(airplane.StateDescription);

            writer_.WriteEndObject();

            // Ensure that the value is written out immediately to the network stream
            writer_.Flush();
            stream_.Flush();
        }
    }
}
