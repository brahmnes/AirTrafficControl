using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using App.Metrics;
using App.Metrics.Filters;
using App.Metrics.AspNetCore;
using App.Metrics.Filtering;
using App.Metrics.Apdex;
using App.Metrics.Counter;
using App.Metrics.Gauge;
using App.Metrics.Histogram;
using App.Metrics.Meter;
using App.Metrics.Timer;
using Validation;

using atc.utilities.AppMetrics;

namespace atc.utilities {
    
    public static class MetricsExtensions {
        public static IWebHostBuilder AddAppMetrics(this IWebHostBuilder builder, string serviceName) {
            Requires.NotNullOrWhiteSpace(serviceName, nameof(serviceName));

            builder.ConfigureMetricsWithDefaults(metricsBuilder => {
                metricsBuilder.Configuration.Configure(metricOptions => {
                    metricOptions.GlobalTags.Add("service-name", serviceName);
                });

                // Filter out internal App.Metrics library metrics
                metricsBuilder.Filter.With(new MetricsFilter().WhereContext(context => context != "appmetrics.internal"));

                metricsBuilder.SampleWith.Reservoir(() => new ForwardDecayingLowWeightThresholdReservoir(
                    sampleSize: 100,
                    alpha: 0.2, // Bias heavily towards lasst 5-6 seconds of sampling; disregard everything older than 20 seconds
                    sampleWeightThreshold: 0.001, // Samples with weight of less than 10% of average should be discarded when rescaling
                    clock: new App.Metrics.Infrastructure.StopwatchClock(),
                    rescaleScheduler: new FixedPeriodReservoirRescaleScheduler(TimeSpan.FromSeconds(30))
                ));

                metricsBuilder.Report.ToInfluxDb("http://localhost:8186", "dbname_unused", TimeSpan.FromSeconds(1));
                // DEBUG builder.Report.ToConsole(TimeSpan.FromSeconds(1));
            })

            .UseMetrics<MinimalRequestTracking>(webHostMetricOptions => {
               webHostMetricOptions.EndpointOptions = (endpointOptions) => {
                   endpointOptions.EnvironmentInfoEndpointEnabled = false;
                   endpointOptions.MetricsEndpointEnabled = false;
                   endpointOptions.MetricsTextEndpointEnabled = true;
               };

               webHostMetricOptions.TrackingMiddlewareOptions = (trackingOptions) => {
                   trackingOptions.ApdexTrackingEnabled = false;
                   // trackingOptions.ApdexTSeconds = 1.0;
                   trackingOptions.IgnoredHttpStatusCodes.Add((int) HttpStatusCode.NotFound);
                   trackingOptions.OAuth2TrackingEnabled = false;
               };
            });

            return builder;
        }
    }
}