# AsyncRequestToSync
A service to transform async requests to sync HTTP requests.

It works as a middleware and just handles requests with 202 status code.
Then, read ``CorrelationId`` from the response header and hold the client request until gets a response asynchronously (or a configured timeout).

![Diagram](docs/images/diagram.jpg)

**Note:** You should not return any Body with 202 status code.

## How to use
You need to register the ``AsyncConnectionHandler`` as a singleton and add ``AsyncRequestToSyncMiddleware`` middleware to the pipeline.

```C#
var builder = WebApplication.CreateBuilder(args);
var connectionHandler = new AsyncConnectionHandler();
servicebus.Subscribe<IMessage>(connectionHandler.HandleMessage) // Register HandleMessage
builder.Services.AddSingleton<IAsyncConnectionHandler>(connectionHandler); // add as singleton
var app = builder.Build();
app.UseMiddleware<AsyncRequestToSyncMiddleware>(); // Register Middleware
app.MapPost("/SampleEndpoint", async (HttpContext httpContext) =>
{
    var correlationId = Guid.NewGuid(); // Generating Unique Id for request

    // TRIGGER/START SOMETHING ASYNC LIKE SENDING A COMMAND TO ANOTHER SERVICE

    httpContext.Response.StatusCode = 202; // returning 202 status code
    httpContext.Response.Headers.Add("CorrelationId", correlationId.ToString("N")); // returnig CorrelationId as header (DO NOT RETURN BODY)
});
```

Also, you can use it in ``YARP``:
```C#
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<AsyncRequestToSyncMiddleware>();
});
```

With some minor changes, you can use this module as a middleware in ``Azure Functions`` isolated worker (but it is not a good practice!).

## Discussion
Sometimes 

Front-end teams usually favor synchronous requests and getting the result of their mutation requests immediately.
However, In some systems, it is not always easy to prepare a synchronous answer for mutation requests, especially when the server needs to do some distributed process.
(For example in Distributed Transactions, Event-sourcing, Event-Driven systems, ...).
The way you are handling this challenge will affect directly UX, UI and Back.

Here are some scenarios that can be used; each has its Pros/Cons:

1. The client sends the command, mocks the response, and updates the UI immediately.

2. The server gets the command, mocks the response, and answers immediately.

3. The server gets the command and returns 202. UI needs to request after a time and hopefully get the result.

4. The server gets the command and returns 202 with a tracking ID.<br />
The client can check the status of the request from another endpoint.

5. Create a bi-directional connection between Client and Service (WebSocket / Server-Sent Events / ...).<br />
Server can send the response to the client when it is ready.

6. Create a long pooling on the Server.<br />
**This middleware is a tool for implementing this method.**