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
        private readonly StreamWriter writer_;
        private readonly JsonSerializer serializer_;

        public AirplaneStatePublisher(Stream stream)
        {
            Requires.NotNull(stream, nameof(stream));

            stream_ = stream;
            writer_ = new StreamWriter(stream_, Encoding.UTF8);
            serializer_ = JsonSerializer.Create(Serialization.GetAtcSerializerSettings());
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

        public void OnNext(Airplane value)
        {
            serializer_.Serialize(writer_, value);

            // Ensure that the value is written out immediately to the network stream
            writer_.Flush();
            stream_.Flush();
        }
    }
}
