using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Filtering;
using App.Metrics.Infrastructure;
using App.Metrics.Scheduling;
using Validation;

using atc.utilities.AppMetrics;

namespace atc.utilities {

	public static class Metrics {
		[Flags]
		public enum Mode {
			Disabled = 0,
			Push = 0x1,
			Pull = 0x2,
			Debug = 0x100
		}

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
            
            builder.ConfigureMetricsWithDefaults(metricsBuilder => {
                metricsBuilder.Configuration.Configure(metricOptions => {
                    metricOptions.GlobalTags.Add("service-name", serviceName);
                });

                // Filter out internal App.Metrics library metrics
                metricsBuilder.Filter.With(new MetricsFilter().WhereContext(context => context != "appmetrics.internal"));

				metricsBuilder.SampleWith.ForwardDecaying(
					sampleSize: 100,
					alpha: 0.1,  // Bias heavily towards lasst 15 seconds of sampling; disregard everything older than 40 seconds
					minimumSampleWeight: 0.001, // Samples with weight of less than 10% of average should be discarded when rescaling
					clock: new StopwatchClock(),
					rescaleScheduler: new DefaultReservoirRescaleScheduler(TimeSpan.FromSeconds(30)));

				if ((MetricsMode & Mode.Push) == Mode.Push) {
    				metricsBuilder.Report.ToInfluxDb(Endpoint, "dbname_unused", TimeSpan.FromSeconds(10));
    			}
				if ((MetricsMode & Mode.Debug) == Mode.Debug) {
					metricsBuilder.Report.ToConsole(TimeSpan.FromSeconds(10));
				}
            })

            .UseMetrics<MinimalRequestTracking>(webHostMetricOptions => {
               webHostMetricOptions.EndpointOptions = (endpointOptions) => {
                   endpointOptions.EnvironmentInfoEndpointEnabled = false;
                   endpointOptions.MetricsEndpointEnabled = true;
                   endpointOptions.MetricsTextEndpointEnabled = true;
               };

               webHostMetricOptions.TrackingMiddlewareOptions = (trackingOptions) => {
                   trackingOptions.ApdexTrackingEnabled = false;
                   // trackingOptions.ApdexTSeconds = 1.0;
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