using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Validation;

using atcsvc.TableStorage;
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
            services.AddMvc().AddJsonOptions(options => {
                options.SerializerSettings.ApplyAtcSerializerSettings();
            });
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
