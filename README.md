# AsyncRequestToSync
A service to transform async requests to sync HTTP requests.

It works as a middleware and tracks HTTP requests for the 202 status code. 
Then, read CorrelationId from the response header and hold the client request until gets a response asynchronously (or configured timeout).

**Note:** You should not return any body with 202 status code.

## How to use
You need to register the ``AsyncConnectionHandler`` as a signletone and add ``AsyncRequestToSyncMiddleware`` middleware to pipline.

```C#
var builder = WebApplication.CreateBuilder(args);
var connectionHandler = new AsyncConnectionHandler();
servicebus.Subscribe<IMessage>(connectionHandler.HandleMessage) // Register HandleMessage
builder.Services.AddSingleton<IAsyncConnectionHandler>(connectionHandler); // add as singletone
var app = builder.Build();
app.UseMiddleware<AsyncRequestToSyncMiddleware>(); // Register Middleware
app.MapPost("/SampleEndpoint", async (HttpContext httpContext) =>
{
    var correlationId = Guid.NewGuid(); // Generating Unique Id for request

    // DO SOMETHING ASYNC LIKE SENDING A COMMAND

    httpContext.Response.StatusCode = 202; // returning 202 status code
    httpContext.Response.Headers.Add("CorrelationId", correlationId.ToString("N")); // returnig CorrelationId as header (DO NOT RETURN BODY)
});
```

Also, you can use it in YARP:
```C#
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<AsyncRequestToSyncMiddleware>();
});
```