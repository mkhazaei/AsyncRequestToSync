using AsyncRequestToSync.Contracts;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace AsyncRequestToSync
{
    public class AsyncConectionHandler : IAsyncConectionHandler
    {
        private const int DEFAULT_REQUEST_TIMEOUT_MS = 25000;

        private readonly ConcurrentDictionary<Guid, InternalConnectionDetail> _connectionPool;
        private readonly int _requestTimeoutInMS;

        public int PoolLenght => _connectionPool.Count;

        public AsyncConectionHandler(int requestTimeoutInMS = DEFAULT_REQUEST_TIMEOUT_MS)
        {
            _connectionPool = new ConcurrentDictionary<Guid, InternalConnectionDetail>();
            _requestTimeoutInMS = requestTimeoutInMS;
        }

        public Task WaitForResponse(HttpContext context, Guid correlationId)
        {
            var requestAborted = context.RequestAborted;
            if (requestAborted.IsCancellationRequested) 
                return Task.CompletedTask; // Aborted

            var tcs = new TaskCompletionSource();
            var timer = new Timer(TimeoutCallback, correlationId, _requestTimeoutInMS, Timeout.Infinite);
            // TODO: consider the situation event recieved earlier than request
            if (!_connectionPool.TryAdd(correlationId, new InternalConnectionDetail(tcs, context, timer)))
            {
                timer.Dispose();
                return Task.CompletedTask;
            }
            requestAborted.Register(RequestAbortedCallback, correlationId);
            return tcs.Task;
        }

        public async Task HandleMessage(IMessage message, CancellationToken cancellationToken)
        { 
            var correlationId = message.CorrelationId;
            if (!_connectionPool.TryRemove(correlationId, out var connection))
                return;
            await WriteReponse(connection.Context!, message, 200, cancellationToken);
            connection.TCS.TrySetResult();
        }

        public async void TimeoutCallback(object? state)
        {
            var correlationId = (state as Guid?) ?? throw new ArgumentException(nameof(state), $"Given {state} is not valid");
            if (!_connectionPool.TryRemove(correlationId, out var connection))
                return;
            connection.Timeout?.Dispose();
            await WriteReponse(connection.Context, new RequestAcceptedResponse(correlationId), 202, connection.Context.RequestAborted);
            connection.TCS.TrySetResult();
        }

        public void RequestAbortedCallback(object? state)
        {
            var correlationId = (state as Guid?) ?? throw new ArgumentException(nameof(state), $"Given {state} is not valid");
            if (!_connectionPool.TryRemove(correlationId, out var connection))
                return;
            connection.Timeout?.Dispose();
            connection.TCS?.TrySetResult();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task WriteReponse(HttpContext context, object message, int statusCode, CancellationToken cancellationToken)
        {
            context.Response.StatusCode = statusCode;
            return context.Response.WriteAsJsonAsync(message, message.GetType(), cancellationToken); // Use IOption<JsonOptions>
        }

    }

    internal record InternalConnectionDetail(TaskCompletionSource TCS, HttpContext Context, Timer Timeout);
}
