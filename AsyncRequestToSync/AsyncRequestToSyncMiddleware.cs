
using Microsoft.AspNetCore.Http;

namespace AsyncRequestToSync
{
    public class AsyncRequestToSyncMiddleware
    {
        private readonly RequestDelegate _next;

        public AsyncRequestToSyncMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IAsyncConectionHandler conectionHandler)
        {
            await _next(context);

            // Hnadle only 202 responses
            if (context.Response.StatusCode != 202)
                return;
            if (!context.Response.Headers.TryGetValue("CorrelationId", out var correlationIdValues))
                return;
            var correlationId = new Guid(correlationIdValues.First());
            await conectionHandler.WaitForResponse(context, correlationId);
        }
    }
}
