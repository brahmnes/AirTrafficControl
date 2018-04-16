using System;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Validation;

namespace atc.utilities 
{
    internal class UrlDependencyFilter : ITelemetryProcessor 
    {
        private string urlToFilter_;
        private ITelemetryProcessor next_;

        public UrlDependencyFilter(ITelemetryProcessor next, string urlToFilter) 
        {
            Requires.NotNull(next, nameof(next));
            Requires.That(Uri.IsWellFormedUriString(urlToFilter, UriKind.Absolute), nameof(urlToFilter), $"{nameof(urlToFilter)} parameter value should be a valid URL");

            next_ = next;
            urlToFilter_ = urlToFilter;
        }

        public void Process(ITelemetry item) {
            DependencyTelemetry telemetry = item as DependencyTelemetry;

            if (telemetry != null && telemetry.Target != null && telemetry.Data.StartsWith(urlToFilter_, StringComparison.Ordinal)) {
                return; // Drop it!
            }

            next_.Process(item);
        }
    }

    public class UrlDependencyFilterFactory : ITelemetryProcessorFactory 
    {
        private string urlToFilter_;

        public UrlDependencyFilterFactory(string urlToFilter)
        {
            Requires.That(Uri.IsWellFormedUriString(urlToFilter, UriKind.Absolute), nameof(urlToFilter), $"{nameof(urlToFilter)} parameter value should be a valid URL");

            urlToFilter_ = urlToFilter;
        }

        public ITelemetryProcessor Create(ITelemetryProcessor next) {
            return new UrlDependencyFilter(next, urlToFilter_);
        }
    }
}