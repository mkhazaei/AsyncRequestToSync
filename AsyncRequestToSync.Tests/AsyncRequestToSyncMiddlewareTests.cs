using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AsyncRequestToSync.Tests
{
    public class AsyncRequestToSyncMiddlewareTests
    {
        [Fact]
        public async Task WhenRequestReturn200()
        {
            var connectionHandler = new Mock<IAsyncConectionHandler>();
            var middleware = new AsyncRequestToSyncMiddleware(_ => Task.CompletedTask);
            var request = Request(200);

            await middleware.InvokeAsync(request.HttpContext, connectionHandler.Object);

            connectionHandler.Verify(m => m.WaitForResponse(It.IsAny<HttpContext>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task WhenRequestReturn202()
        {
            var connectionHandler = new Mock<IAsyncConectionHandler>();
            var middleware = new AsyncRequestToSyncMiddleware(_ => Task.CompletedTask);
            var request = Request(202);

            await middleware.InvokeAsync(request.HttpContext, connectionHandler.Object);

            connectionHandler.Verify(m => m.WaitForResponse(request.HttpContext, request.CorrelationId), Times.Once);
        }

        [Fact]
        public async Task WhereCorrelationIdIsNotExist()
        {
            var connectionHandler = new Mock<IAsyncConectionHandler>();
            var middleware = new AsyncRequestToSyncMiddleware(_ => Task.CompletedTask);
            var request = Request(202);
            request.HttpContext.Response.Headers.Remove("CorrelationId");

            await middleware.InvokeAsync(request.HttpContext, connectionHandler.Object);

            connectionHandler.Verify(m => m.WaitForResponse(It.IsAny<HttpContext>(), It.IsAny<Guid>()), Times.Never);
        }

        private (Guid CorrelationId, HttpContext HttpContext) Request(int statusCode)
        {
            var correlationId = Guid.NewGuid();
            var httpContext = new DefaultHttpContext();
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.Headers.Add("CorrelationId", correlationId.ToString("N"));

            return (correlationId, httpContext);
        }
    }
}
