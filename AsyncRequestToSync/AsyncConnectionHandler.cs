using AsyncRequestToSync.Contracts;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace AsyncRequestToSync
{
    public class AsyncConnectionHandler : IAsyncConectionHandler
    {
        private const int DEFAULT_REQUEST_TIMEOUT_MS = 25000;

        private readonly ConcurrentDictionary<Guid, InternalConnectionDetail> _connectionPool;
        private readonly int _requestTimeoutInMS;

        public int PoolLength => _connectionPool.Count;

        public AsyncConnectionHandler(int requestTimeoutInMS = DEFAULT_REQUEST_TIMEOUT_MS)
        {
            _connectionPool = new ConcurrentDictionary<Guid, InternalConnectionDetail>();
            _requestTimeoutInMS = requestTimeoutInMS;
        }

        public Task WaitForResponse(HttpContext context, Guid correlationId)
        {
            var requestAborted = context.RequestAborted;
            if (requestAborted.IsCancellationRequested) 
                return Task.CompletedTask; // Aborted
            if (context.Response.HasStarted)
                return Task.CompletedTask; // Need response buffering of requests (Performance!)

            var tcs = new TaskCompletionSource();
            var timer = new Timer(TimeoutCallback, correlationId, _requestTimeoutInMS, Timeout.Infinite);
            // check if event of request completion received sooner
            var connection = _connectionPool.GetOrAdd(correlationId, new InternalConnectionDetail(tcs, context, timer, null))!;
            if (connection.response != null) // Result Is Ready (event of request completion received sooner)
            {
                timer.Dispose();
                connection.Timeout?.Dispose();
                _connectionPool.TryRemove(correlationId, out _); // clear pool.
                return WriteResponse(context, connection.response, 200, requestAborted);
            }
            requestAborted.Register(RequestAbortedCallback, correlationId);
            return tcs.Task;
        }

        public async Task HandleMessage(IMessage message, CancellationToken cancellationToken)
        { 

            var correlationId = message.CorrelationId;
            InternalConnectionDetail? connection;
            while (true)
            {
                // is request detail added to pool by WaitForResponse? OR another message received (most service bus guarantee atleast one delivery)?
                if (_connectionPool.TryRemove(correlationId, out connection) && connection.RequestReceived())
                    break;
                // try to add the response to pool
                var timer = connection?.Timeout ?? new Timer(TimeoutCallback, correlationId, _requestTimeoutInMS, Timeout.Infinite);
                connection ??= new InternalConnectionDetail(null, null, timer, message);
                if (_connectionPool.TryAdd(correlationId, connection))
                    return; // request added to pool. wait for WaitForResponse.
                timer?.Dispose(); // in the meantime, request added by WaitForResponse or another event.
            }
            connection.Timeout?.Dispose();
            if (connection.Context == null || connection.TCS == null)
                return;
            await WriteResponse(connection.Context!, message, 200, cancellationToken);
            connection.TCS.TrySetResult();
        }

        public async void TimeoutCallback(object? state)
        {
            var correlationId = (state as Guid?) ?? throw new ArgumentException(nameof(state), $"Given {state} is not valid");
            if (!_connectionPool.TryRemove(correlationId, out var connection))
                return;
            connection.Timeout?.Dispose();
            if (connection.Context == null || connection.TCS == null)
                return;
            await WriteResponse(connection.Context, new RequestAcceptedResponse(correlationId), 202, connection.Context.RequestAborted);
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
        private Task WriteResponse(HttpContext context, object message, int statusCode, CancellationToken cancellationToken)
        {
            if (context.Response.HasStarted) 
                return Task.CompletedTask; // Need response buffering of requests (Performance!)
            context.Response.StatusCode = statusCode;
            context.Response.ContentLength = null; // Kestrel Writer Stream doesn't support Body.Length (writing to the network as fast as possible) + JsonSerializer.SerializeAsync(Stream ...) doesn't accumulate serialized object length
            return context.Response.WriteAsJsonAsync(message, message.GetType(), cancellationToken); // Use IOption<JsonOptions>
        }

    }

    internal record InternalConnectionDetail(TaskCompletionSource? TCS, HttpContext? Context, Timer? Timeout, object? response)
    {
        public bool RequestReceived() => Context != null && TCS != null;
    }
}
