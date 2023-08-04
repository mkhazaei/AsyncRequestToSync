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
    public class AsyncConnectionHandlerTests
    {
        [Fact]
        public async Task WhenThereIsNoResponse()
        {
            var connectionHandler = new AsyncConnectionHandler(2000);
            var request = Request();

            var watch = Stopwatch.StartNew();
            await connectionHandler.WaitForResponse(request.HttpContext, request.CorrelationId);
            watch.Stop();
            var body = await ResponseBody<SampleResponse>(request.HttpContext);

            Assert.Equal(0, connectionHandler.PoolLength);
            Assert.Equal(2, Math.Round(watch.Elapsed.TotalSeconds));
            Assert.Equal(202, request.HttpContext.Response.StatusCode);
            Assert.Equal(request.CorrelationId, body?.CorrelationId);
        }

        [Fact]
        public async Task WhenThereIsAResponse()
        {
            var connectionHandler = new AsyncConnectionHandler(4000);
            var request = Request();

            var watch = Stopwatch.StartNew();
            _ = new Timer(async _ => await connectionHandler.HandleMessage(new SampleResponse(request.CorrelationId, "Data1"), CancellationToken.None), null, 2000, -1);
            await connectionHandler.WaitForResponse(request.HttpContext, request.CorrelationId);
            watch.Stop();
            var body = await ResponseBody<SampleResponse>(request.HttpContext);

            Assert.Equal(0, connectionHandler.PoolLength);
            Assert.Equal(2, Math.Round(watch.Elapsed.TotalSeconds));
            Assert.Equal(200, request.HttpContext.Response.StatusCode); 
            Assert.Equal(request.CorrelationId, body?.CorrelationId);
            Assert.Equal("Data1", body?.Data);
        }

        [Fact]
        public async Task WhenResponseReceivedSooner()
        {
            var connectionHandler = new AsyncConnectionHandler(4000);
            var request = Request();

            var watch = Stopwatch.StartNew();
            await connectionHandler.HandleMessage(new SampleResponse(request.CorrelationId, "Data1"), CancellationToken.None);
            await Task.Delay(1000);
            await connectionHandler.WaitForResponse(request.HttpContext, request.CorrelationId);
            watch.Stop();
            var body = await ResponseBody<SampleResponse>(request.HttpContext);

            Assert.Equal(0, connectionHandler.PoolLength);
            Assert.Equal(1, Math.Round(watch.Elapsed.TotalSeconds));
            Assert.Equal(200, request.HttpContext.Response.StatusCode);
            Assert.Equal(request.CorrelationId, body?.CorrelationId);
            Assert.Equal("Data1", body?.Data);
        }

        [Fact]
        public async Task WhenMoreThanOneResponseReceivedSooner()
        {
            var connectionHandler = new AsyncConnectionHandler(4000);
            var request = Request();

            var watch = Stopwatch.StartNew();
            await connectionHandler.HandleMessage(new SampleResponse(request.CorrelationId, "Data1-1"), CancellationToken.None);
            await Task.Delay(1000);
            await connectionHandler.HandleMessage(new SampleResponse(request.CorrelationId, "Data1-2"), CancellationToken.None);
            Assert.Equal(1, connectionHandler.PoolLength);
            await Task.Delay(1000);
            await connectionHandler.WaitForResponse(request.HttpContext, request.CorrelationId);
            watch.Stop();
            var body = await ResponseBody<SampleResponse>(request.HttpContext);

            Assert.Equal(0, connectionHandler.PoolLength);
            Assert.Equal(2, Math.Round(watch.Elapsed.TotalSeconds));
            Assert.Equal(200, request.HttpContext.Response.StatusCode);
            Assert.Equal(request.CorrelationId, body?.CorrelationId);
            Assert.Equal("Data1-1", body?.Data);
        }

        [Fact]
        public async Task WhenThereAreMoreThanOneResponse()
        {
            var connectionHandler = new AsyncConnectionHandler(2000);
            var request = Request();

            var watch = Stopwatch.StartNew();
            _ = new Timer(async _ => await connectionHandler.HandleMessage(new SampleResponse(request.CorrelationId, "Data1-1"), CancellationToken.None), null, 1000, -1);
            _ = new Timer(async _ => await connectionHandler.HandleMessage(new SampleResponse(request.CorrelationId, "Data1-2"), CancellationToken.None), null, 2000, -1);
            _ = new Timer(async _ => await connectionHandler.HandleMessage(new SampleResponse(request.CorrelationId, "Data1-3"), CancellationToken.None), null, 3000, -1);
            await connectionHandler.WaitForResponse(request.HttpContext, request.CorrelationId);
            watch.Stop();
            await Task.Delay(2500);
            Assert.Equal(1, connectionHandler.PoolLength);
            await Task.Delay(1000);
            var body = await ResponseBody<SampleResponse>(request.HttpContext);

            Assert.Equal(0, connectionHandler.PoolLength);
            Assert.Equal(1, Math.Round(watch.Elapsed.TotalSeconds));
            Assert.Equal(200, request.HttpContext.Response.StatusCode);
            Assert.Equal(request.CorrelationId, body?.CorrelationId);
            Assert.Equal("Data1-1", body?.Data);
        }

        [Fact]
        public async Task WhereThereIsMoreThanOneRequest()
        {
            var connectionHandler = new AsyncConnectionHandler(2000);
            var request1 = Request();
            var request2 = Request();
            var request3 = Request();

            _ = new Timer(async _ => await connectionHandler.HandleMessage(new SampleResponse(request1.CorrelationId, "Data1"), CancellationToken.None), null, 1000, -1);
            _ = new Timer(async _ => await connectionHandler.HandleMessage(new SampleResponse(request2.CorrelationId, "Data2"), CancellationToken.None), null, 1000, -1);
            await Task.WhenAll(connectionHandler.WaitForResponse(request1.HttpContext, request1.CorrelationId),
                connectionHandler.WaitForResponse(request2.HttpContext, request2.CorrelationId),
                connectionHandler.WaitForResponse(request3.HttpContext, request3.CorrelationId));
            var body1 = await ResponseBody<SampleResponse>(request1.HttpContext);
            var body2 = await ResponseBody<SampleResponse>(request2.HttpContext);
            var body3 = await ResponseBody<RequestAcceptedResponse>(request3.HttpContext);

            Assert.Equal(0, connectionHandler.PoolLength);
            Assert.Equal(200, request1.HttpContext.Response.StatusCode);
            Assert.Equal(200, request2.HttpContext.Response.StatusCode);
            Assert.Equal(202, request3.HttpContext.Response.StatusCode);
            Assert.Equal(request1.CorrelationId, body1?.CorrelationId);
            Assert.Equal("Data1", body1?.Data);
            Assert.Equal(request2.CorrelationId, body2?.CorrelationId);
            Assert.Equal("Data2", body2?.Data);
            Assert.Equal(request3.CorrelationId, body3?.CorrelationId);
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