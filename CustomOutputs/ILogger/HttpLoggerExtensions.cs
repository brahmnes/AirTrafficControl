using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Logging
{
    public static class HttpLoggerExtensions
    {
        /// <summary>
        /// Add http logger to ILogger
        /// </summary>
        /// <param name="endpoint">The http endpoint</param>
        /// <param name="appendCategoryToEndpoint">Whether append the category name to the http endpoint. If it's appended, it will be used by fluentd http input as the tag</param>
        public static ILoggingBuilder AddHttp(this ILoggingBuilder builder, string endpoint = "http://localhost:8887", bool appendCategoryToEndpoint = false)
        {
            ServiceCollectionServiceExtensions.AddSingleton<ILoggerProvider, HttpLoggerProvider>(builder.Services,
                provider =>
                {
                    return new HttpLoggerProvider(endpoint, appendCategoryToEndpoint);
                });
            return builder;
        }

        /// <summary>
        /// Add http logger to ILogger
        /// </summary>
        /// <param name="endpoint">The http endpoint</param>
        /// <param name="appendCategoryToEndpoint">Whether append the category name to the http endpoint. If it's appended, it will be used by fluentd http input as the tag</param>
        public static ILoggerFactory AddHttp(this ILoggerFactory factory, string endpoint = "http://localhost:8887", bool appendCategoryToEndpoint = false)
        {
            factory.AddProvider(new HttpLoggerProvider(endpoint, appendCategoryToEndpoint));
            return factory;
        }
    }
}
