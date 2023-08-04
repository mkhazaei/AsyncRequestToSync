using AsyncRequestToSync.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace AsyncRequestToSync.Tests
{
    public class AsyncConectionHandlerTests
    {
        [Fact]
        public async Task WhenThereIsNoResponse()
        {
            var connectionHandler = new AsyncConectionHandler(2000);
            var request = Request();

            var watch = Stopwatch.StartNew();
            await connectionHandler.WaitForResponse(request.HttpContext, request.CorrelationId);
            watch.Stop();
            var body = await ResponseBody<SampleResponse>(request.HttpContext);

            Assert.Equal(0, connectionHandler.PoolLenght);
            Assert.Equal(2, Math.Round(watch.Elapsed.TotalSeconds));
            Assert.Equal(202, request.HttpContext.Response.StatusCode);
            Assert.Equal(request.CorrelationId, body?.CorrelationId);
        }

        [Fact]
        public async Task WhenThereIsAResponse()
        {
            var connectionHandler = new AsyncConectionHandler(4000);
            var request = Request();

            var watch = Stopwatch.StartNew();
            _ = new Timer(async _ => await connectionHandler.HandleMessage(new SampleResponse(request.CorrelationId, "Data1"), CancellationToken.None), null, 2000, -1);
            await connectionHandler.WaitForResponse(request.HttpContext, request.CorrelationId);
            watch.Stop();
            var body = await ResponseBody<SampleResponse>(request.HttpContext);

            Assert.Equal(0, connectionHandler.PoolLenght);
            Assert.Equal(2, Math.Round(watch.Elapsed.TotalSeconds));
            Assert.Equal(200, request.HttpContext.Response.StatusCode); 
            Assert.Equal(request.CorrelationId, body?.CorrelationId);
            Assert.Equal("Data1", body?.Data);
        }

        [Fact]
        public Task WhenResponseRecievedSooner()
        {
            throw new NotImplementedException();
        }



        internal record SampleResponse(Guid CorrelationId, string Data) : IMessage;

        private (Guid CorrelationId, HttpContext HttpContext) Request()
        {
            var correlationId = Guid.NewGuid();
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();

            return (correlationId, httpContext);
        }

        private ValueTask<TBody?> ResponseBody<TBody>(HttpContext httpContext)
        {
            httpContext.Response.Body.Position = 0;
            return JsonSerializer.DeserializeAsync<TBody>(httpContext.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }


    }
}