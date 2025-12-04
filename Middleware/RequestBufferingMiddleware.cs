using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Cps.S3Spike.Middleware;

/// <summary>
/// Middleware that enables request body buffering to support YARP proxy retries.
/// </summary>
public class RequestBufferingMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Get the HttpContext from the function context
        var httpContext = context.GetHttpContext();
        
        if (httpContext != null)
        {
            // Enable buffering so the request body can be read multiple times
            httpContext.Request.EnableBuffering();

        }

        await next(context);
    }
}