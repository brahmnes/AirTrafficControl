using System;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

using atc.utilities;

namespace airplanesvc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) {
            var builder = WebHost.CreateDefaultBuilder(args);

            if (Metrics.Enabled) {
                builder = builder.AddAppMetrics(nameof(airplanesvc));
            }
            
            return builder
                .UseStartup<Startup>()
                .UseApplicationInsights()
                .Build();
        }
        

    }
}
