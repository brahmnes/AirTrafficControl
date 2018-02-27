using Microsoft.Extensions.Logging.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Validation;

namespace Microsoft.Extensions.Logging
{
    public class HttpLogger : ILogger, IDisposable
    {
        private Uri endpoint;
        private string categoryName;
        private HttpClient client;

        public HttpLogger(Uri endpoint, string categoryName, bool appendCategoryToEndpoint)
        {
            // TODO: Add log level or filter function

            this.endpoint = appendCategoryToEndpoint ? new Uri(endpoint, categoryName) : endpoint;
            this.categoryName = categoryName;
            this.client = new HttpClient();
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return HttpLoggerScope.Push(state);
        }

        public void Dispose()
        {
            this.client.Dispose();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Requires.NotNull(formatter, nameof(formatter));

            if (!this.IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var properties = new Dictionary<string, object>();
            if (state is FormattedLogValues)
            {
                var formattedState = state as FormattedLogValues;

                //last KV is the whole message, we will pass it separately
                for (int i = 0; i < formattedState.Count - 1; i++)
                {
                    properties.Add(formattedState[i].Key, formattedState[i].Value);
                }
            }

            GetScopeInformation(properties);

            var messageObject = new
            {
                logLevel,
                eventId,
                message,
                this.categoryName,
                properties
            };
            var jsonMessage = JsonConvert.SerializeObject(messageObject);

            this.client.PostAsync(this.endpoint, new StringContent(jsonMessage, Encoding.UTF8, "application/json"));
        }

        private void GetScopeInformation(Dictionary<string, object> properties)
        {
            var scope = HttpLoggerScope.Current;
            var scopeValueStack = new Stack<string>();
            while (scope != null)
            {
                if (scope.State != null)
                {
                    var formattedState = scope.State as FormattedLogValues;
                    if (formattedState != null)
                    {
                        for (int i = 0; i < formattedState.Count - 1; i++)
                        {
                            KeyValuePair<string, object> current = formattedState[i];
                            properties.Add(current.Key, current.Value);
                        }

                        //last KV is the whole 'scope' message, we will add it formatted
                        scopeValueStack.Push(formattedState.ToString());
                    }
                    else
                    {
                        scopeValueStack.Push(scope.State.ToString());
                    }
                }

                scope = scope.Parent;
            }

            if (scopeValueStack.Count > 0)
            {
                properties.Add("Scope", string.Join("\r\n", scopeValueStack));
            }
        }
    }
}
