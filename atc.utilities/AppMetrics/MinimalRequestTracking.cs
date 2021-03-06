﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace atc.utilities.AppMetrics {
    
    public class MinimalRequestTracking : IStartupFilter {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) {
            return (IApplicationBuilder app) => {
                app.UseMetricsActiveRequestMiddleware();
                app.UseMetricsErrorTrackingMiddleware();
                app.UseMetricsRequestTrackingMiddleware();

                if (Metrics.MetricsMode.HasFlag(Metrics.Mode.Pull)) {
                    app.UseMetricsEndpoint();
                    app.UseMetricsTextEndpoint();
                }

                next(app);
            };
        }
    }

}
