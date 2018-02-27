using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Logging
{
    public class HttpLoggerProvider : ILoggerProvider
    {
        private readonly string endpoint;
        private readonly bool appendCategoryToEndpoint;
        private HttpLogger logger;

        public HttpLoggerProvider(string endpoint, bool appendCategoryToEndpoint)
        {
            this.endpoint = endpoint;
            this.appendCategoryToEndpoint = appendCategoryToEndpoint;
        }

        public ILogger CreateLogger(string categoryName)
        {
            // append category name as tag
            categoryName = categoryName ?? "ILoggerHttpLogger";
            this.logger = new HttpLogger(new Uri(endpoint), categoryName, appendCategoryToEndpoint);

            return this.logger;
        }

        public void Dispose()
        {
            this.logger.Dispose();
        }
    }
}
