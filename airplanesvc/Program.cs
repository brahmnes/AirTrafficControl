using System;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using App.Metrics;
using App.Metrics.AspNetCore;

namespace airplanesvc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .ConfigureMetricsWithDefaults(builder => {
                builder.Configuration.Configure(metricOptions => {
                    metricOptions.GlobalTags.Add("service-name", nameof(airplanesvc));
                });

                builder.Report.ToInfluxDb("http://localhost:8186", "dbname_unused");
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
            })
            .UseStartup<Startup>()
            .UseApplicationInsights()
            .Build();

    }
}
