using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Validation;


namespace atcsvc
{
    public class PushStreamResult : IActionResult
    {
        private readonly Func<Stream, CancellationToken, Task> onStreamAvailable_;
        private readonly string contentType_;

        public PushStreamResult(string contentType, Func<Stream, CancellationToken, Task> onStreamAvailable)
        {
            Requires.NotNull(onStreamAvailable, nameof(onStreamAvailable));
            Requires.NotNullOrWhiteSpace(contentType, nameof(contentType));

            onStreamAvailable_ = onStreamAvailable;
            contentType_ = contentType;
        }
        public Task ExecuteResultAsync(ActionContext context)
        {
            var stream = context.HttpContext.Response.Body;
            context.HttpContext.Response.ContentType = contentType_;
            return onStreamAvailable_(stream, context.HttpContext.RequestAborted);
        }
    }
}
