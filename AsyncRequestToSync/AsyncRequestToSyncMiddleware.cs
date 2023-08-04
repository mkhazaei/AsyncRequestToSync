
using Microsoft.AspNetCore.Http;

namespace AsyncRequestToSync
{
    public class AsyncRequestToSyncMiddleware
    {
        private readonly RequestDelegate _next;

        public AsyncRequestToSyncMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IAsyncConnectionHandler connectionHandler)
        {
            await _next(context);

            // Handle only 202 responses and ignore request that Response Has Started (you are not allowed to change)
            if (context.Response.StatusCode != StatusCodes.Status202Accepted || context.Response.HasStarted)
                return;
            // get CorrelationId from header
            if (!context.Response.Headers.TryGetValue("CorrelationId", out var correlationIdValues))
                return;
            var correlationId = new Guid(correlationIdValues.First());
            await connectionHandler.WaitForResponse(context, correlationId);
        }
    }
}
