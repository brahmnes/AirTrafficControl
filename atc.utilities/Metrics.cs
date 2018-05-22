using System;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Filtering;
using App.Metrics.Formatters;
using App.Metrics.Formatters.Prometheus;
using App.Metrics.Infrastructure;
using App.Metrics.Scheduling;
using App.Metrics.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Validation;

namespace atc.utilities {

    public static class Metrics {
        [Flags]
        public enum Mode {
            Disabled = 0,
            Push = 0x1,
            Pull = 0x2,
            Debug = 0x100
        }

        private static IMetricsRoot MetricsRoot;

        public static bool Enabled => Metrics.MetricsMode != Mode.Disabled;

        public static string Endpoint {
            get {
                string port = Environment.GetEnvironmentVariable("METRICS_PORT");
                if (int.TryParse(port, out int unused)) {
                    return $"http://localhost:{port.Trim()}";
                }
                else return null;
            }
        }

        public static Mode MetricsMode {
            get {
                string mode = Environment.GetEnvironmentVariable("METRICS_MODE");
                if (string.IsNullOrEmpty(mode)) {
                    return Mode.Disabled;
                }

                return mode.IndexOf("pull", StringComparison.OrdinalIgnoreCase) >= 0 ? Mode.Pull : Mode.Push;
            }
        } 

        public static IWebHostBuilder AddAppMetrics(this IWebHostBuilder builder, string serviceName) {
            Requires.NotNullOrWhiteSpace(serviceName, nameof(serviceName));

            if (!Metrics.Enabled) {
                return builder;
            }

            builder.ConfigureServices((context, services) => {
                var metricsBuilder = App.Metrics.AppMetrics.CreateDefaultBuilder();

                if (MetricsMode.HasFlag(Mode.Pull)) {
                    metricsBuilder
                        .OutputMetrics.AsPrometheusPlainText()
                        .OutputMetrics.AsPrometheusProtobuf();
                }

                metricsBuilder.Configuration.Configure(metricOptions => {
                    metricOptions.GlobalTags.Add("service-name", serviceName);
                });

                // Filter out internal App.Metrics library metrics
                metricsBuilder.Filter.With(new MetricsFilter().WhereContext(filterContext => filterContext != "appmetrics.internal"));

                metricsBuilder.SampleWith.ForwardDecaying(
                    sampleSize: 100,
                    alpha: 0.1,  // Bias heavily towards lasst 15 seconds of sampling; disregard everything older than 40 seconds
                    minimumSampleWeight: 0.001, // Samples with weight of less than 10% of average should be discarded when rescaling
                    clock: new StopwatchClock(),
                    rescaleScheduler: new DefaultReservoirRescaleScheduler(TimeSpan.FromSeconds(30)));

                if (MetricsMode.HasFlag(Mode.Push)) {
                    metricsBuilder.Report.ToInfluxDb(Endpoint, "dbname_unused", TimeSpan.FromSeconds(10));
                }
                if (MetricsMode.HasFlag(Mode.Debug)) {
                    metricsBuilder.Report.ToConsole(TimeSpan.FromSeconds(10));
                }

                metricsBuilder.Configuration.ReadFrom(context.Configuration);
                services.AddMetrics(metricsBuilder);

                MetricsRoot = metricsBuilder.Build();
            })
                
            .ConfigureMetrics()
            .UseMetrics<AppMetrics.MinimalRequestTracking>(webHostMetricOptions => {
               webHostMetricOptions.EndpointOptions = (endpointOptions) => {
                   endpointOptions.EnvironmentInfoEndpointEnabled = false;
                   endpointOptions.MetricsEndpointEnabled = true;
                   endpointOptions.MetricsTextEndpointEnabled = true;

                   if (MetricsMode.HasFlag(Mode.Pull)) {
                        Debug.Assert(MetricsRoot != null);
                        endpointOptions.MetricsTextEndpointOutputFormatter = MetricsRoot.OutputMetricsFormatters.GetType<MetricsPrometheusTextOutputFormatter>();
                        endpointOptions.MetricsEndpointOutputFormatter = MetricsRoot.OutputMetricsFormatters.GetType<MetricsPrometheusProtobufOutputFormatter>();
                    }
               };

               webHostMetricOptions.TrackingMiddlewareOptions = (trackingOptions) => {
                   trackingOptions.ApdexTrackingEnabled = false;
                   trackingOptions.IgnoredHttpStatusCodes.Add((int) HttpStatusCode.NotFound);
                   trackingOptions.OAuth2TrackingEnabled = false;

                   // Ignore health queries
                   trackingOptions.IgnoredRoutesRegexPatterns.Add(@"health/?\s*$");
               };
            });

            return builder;
        }
    }
}