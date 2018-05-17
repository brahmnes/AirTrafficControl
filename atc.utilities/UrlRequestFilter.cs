using System;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Validation;

namespace atc.utilities 
{
    internal class UrlRequestFilter : ITelemetryProcessor 
    {
        private string pathToFilter_;
        private ITelemetryProcessor next_;

        public UrlRequestFilter(ITelemetryProcessor next, string pathToFilter) 
        {
            Requires.NotNull(next, nameof(next));
            Requires.NotNullOrWhiteSpace(nameof(pathToFilter), "Path to filter should not be empty");

            next_ = next;
            pathToFilter_ = pathToFilter;
        }

        public void Process(ITelemetry item) {
            RequestTelemetry telemetry = item as RequestTelemetry;

            if (telemetry != null && telemetry.Url != null && telemetry.Url.PathAndQuery.StartsWith(pathToFilter_, StringComparison.Ordinal)) {
                return; // Drop it!
            }

            next_.Process(item);
        }
    }

    public class UrlRequestFilterFactory : ITelemetryProcessorFactory 
    {
        private string pathToFilter_;

        public UrlRequestFilterFactory(string pathToFilter)
        {
            Requires.NotNullOrWhiteSpace(nameof(pathToFilter), "Path to filter should not be empty");

            pathToFilter_ = pathToFilter;
        }

        public ITelemetryProcessor Create(ITelemetryProcessor next) {
            return new UrlRequestFilter(next, pathToFilter_);
        }
    }
}