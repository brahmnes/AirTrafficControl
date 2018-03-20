using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Extensibility;

using AirTrafficControl.Interfaces;
using CustomOutputs.ApplicationInsights;

namespace airplanesvc
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services
                // Temporarily disable metrics upload till overreporting is resolved
                //.AddMvc(options => options.AddMetricsResourceFilter())
                .AddMvc()
                .AddJsonOptions(options => options.SerializerSettings.ApplyAtcSerializerSettings());
            
            services.AddSingleton<AirplaneRepository>(serviceProvider => new AirplaneRepository());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();

            var config = app.ApplicationServices.GetService<TelemetryConfiguration>();
            config.DefaultTelemetrySink.TelemetryChannel = new HttpChannel();
        }
    }
}
