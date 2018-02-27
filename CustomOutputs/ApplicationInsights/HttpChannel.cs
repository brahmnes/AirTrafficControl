using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using System;
using System.Net.Http;
using System.Text;

namespace CustomOutputs.ApplicationInsights
{
    public class HttpChannel : ITelemetryChannel
    {
        private HttpClient client;
        private Uri requestUri;

        /// <summary>
        /// HttpChannel for application insights
        /// </summary>
        /// <param name="endpoint">The endpoint of the HttpChannel. Channel name is appended, which will be used by fluentd as tag</param>
        public HttpChannel(string endpoint = "http://localhost:8887/ApplicationInsightsHttpChannel")
        {
            this.EndpointAddress = endpoint;
            this.client = new HttpClient();
        }

        public bool? DeveloperMode { get; set; }
        public string EndpointAddress
        {
            get { return this.requestUri.ToString(); }
            set { this.requestUri = new Uri(value); }
        }

        public void Dispose()
        {
            this.client.Dispose();
        }

        public void Flush()
        {
        }

        public void Send(ITelemetry item)
        {
            // TODO: add buffer and send in batch
            var buffer = JsonSerializer.Serialize(new[] { item }, compress: false);
            var content = Encoding.UTF8.GetString(buffer, 0, buffer.Length);

            client.PostAsync(this.requestUri, new StringContent(content, Encoding.UTF8, "application/json"));
        }
    }
}
