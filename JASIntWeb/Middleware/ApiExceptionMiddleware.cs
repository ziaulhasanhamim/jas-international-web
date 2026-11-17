namespace JASIntWeb.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller has disconnected; there is no response left to write.
        }
        catch (Exception error) when (!context.Response.HasStarted)
        {
            var status = error is BadHttpRequestException badRequest
                ? badRequest.StatusCode
                : StatusCodes.Status500InternalServerError;
            logger.LogError(error, "API request failed: {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.Clear();
            context.Response.Headers.CacheControl = "no-store";
            await Results.Problem(statusCode: status,
                title: status < 500 ? "Invalid request" : "The request could not be completed.",
                extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
                .ExecuteAsync(context);
        }
    }
}
