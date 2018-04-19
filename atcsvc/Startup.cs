using System;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.AspNetCore;

using CustomOutputs.ApplicationInsights;

using atc.utilities;
using AirTrafficControl.Interfaces;

namespace atcsvc
{
    public class Startup
    {
        private readonly ISubject<Airplane> airplaneStateEventAggregator_;
        private AtcSvc atcSvc_;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            airplaneStateEventAggregator_ = new Subject<Airplane>();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc(options => options.AddMetricsResourceFilter())
                .AddJsonOptions(options => options.SerializerSettings.ApplyAtcSerializerSettings());

            if (Metrics.Enabled) {
                services.AddSingleton<ITelemetryProcessorFactory>(sp => new UrlDependencyFilterFactory(Metrics.Endpoint));
            }

            services.AddSingleton<ISubject<Airplane>>(airplaneStateEventAggregator_);
            services.AddSingleton<AtcSvc>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime, IServiceProvider serviceProvider)
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

            atcSvc_ = serviceProvider.GetService<AtcSvc>();

            var config = app.ApplicationServices.GetService<TelemetryConfiguration>();
            config.DefaultTelemetrySink.TelemetryChannel = new HttpChannel();
        }

        private void OnApplicationStarted()
        {
        }

        private void OnApplicationStopping()
        {
            atcSvc_.AsyncDispose().GetAwaiter().GetResult();
        }
    }
}
