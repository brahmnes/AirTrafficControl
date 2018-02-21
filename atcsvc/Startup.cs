using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

using atcsvc.TableStorage;


namespace atcsvc
{
    public class Startup
    {
        private enum TimePassageHandling: int
        {
            Completed = 0,
            InProgress = 1
        }
        private const int InvalidTime = -1;
        private readonly TimeSpan WorldTimerPeriod = TimeSpan.FromSeconds(5);

        private Timer worldTimer_;
        private int timePassageHandling_ = (int) TimePassageHandling.Completed;
        private FlyingAirplanesTable flyingAirplanesTable_;
        private WorldStateTable worldStateTable_;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();

            appLifetime.ApplicationStarted.Register(OnApplicationStarted);
            appLifetime.ApplicationStopping.Register(OnApplicationStopping);
            Console.CancelKeyPress += (sender, args) =>
            {
                appLifetime.StopApplication();
                // Don't terminate the process immediately, wait for the Main thread to exit gracefully.
                args.Cancel = true;
            };
        }

        private void OnApplicationStarted()
        {
            flyingAirplanesTable_ = new FlyingAirplanesTable(Configuration);
            worldStateTable_ = new WorldStateTable(Configuration);

            worldTimer_?.Dispose();
            worldTimer_ = new Timer(OnTimePassed, null, TimeSpan.FromSeconds(1), WorldTimerPeriod);
        }

        private void OnApplicationStopping()
        {
            worldTimer_.Dispose();
            worldTimer_ = null;
        }

        private void OnTimePassed(object state)
        {
            Task.Run(async () => 
            {
                // int currentTime = InvalidTime;

                if (Interlocked.CompareExchange(ref timePassageHandling_, (int) TimePassageHandling.InProgress, (int) TimePassageHandling.Completed) == (int) TimePassageHandling.InProgress)
                {
                    // Time passage handling took longer than expected, let bail out and wait for next timer tick.
                    return;
                }

                try
                {
                    var flyingAirplaneCallSigns = await flyingAirplanesTable_.GetFlyingAirplaneCallSignsAsync(CancellationToken.None);
                    if (!flyingAirplaneCallSigns.Any())
                    {
                        return; // Nothing to do
                    }

                    var worldState = await worldStateTable_.GetWorldStateAsync()
                    // TODO: query flying airplane states and instruct them as necessary
                    // TODO: make sure the clients inquring about airplane states get a consistent view

                }
                finally 
                {
                    timePassageHandling_ = (int) TimePassageHandling.Completed;
                }
            });
        }
    }
}
