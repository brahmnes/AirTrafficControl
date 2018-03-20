using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using App.Metrics;
using App.Metrics.AspNetCore;
using Validation;

namespace atc.utilities {
    
    public static class MetricsExtensions {
        public static IWebHostBuilder AddAppMetrics(this IWebHostBuilder builder, string serviceName) {
            Requires.NotNullOrWhiteSpace(serviceName, nameof(serviceName));

            builder.ConfigureMetricsWithDefaults(metricsBuilder => {
                metricsBuilder.Configuration.Configure(metricOptions => {
                    metricOptions.GlobalTags.Add("service-name", serviceName);
                });

                metricsBuilder.Report.ToInfluxDb("http://localhost:8186", "dbname_unused", TimeSpan.FromSeconds(1));
                // DEBUG builder.Report.ToConsole(TimeSpan.FromSeconds(1));
            })

            .UseMetrics(webHostMetricOptions => {
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