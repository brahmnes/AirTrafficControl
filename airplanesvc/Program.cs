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

        public static IWebHost BuildWebHost(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            // Temporarily disable metrics upload till overreporting is resolved
            // .AddAppMetrics(nameof(airplanesvc))
            .UseStartup<Startup>()
            .UseApplicationInsights()
            .Build();

    }
}
