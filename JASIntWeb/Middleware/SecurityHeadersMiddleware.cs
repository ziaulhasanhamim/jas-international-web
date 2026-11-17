namespace JASIntWeb.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;
        var headers = response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        var path = context.Request.Path;
        if (path.StartsWithSegments("/account")
            || path.StartsWithSegments("/admin")
            || path.StartsWithSegments("/checkout")
            || path.StartsWithSegments("/order"))
        {
            response.OnStarting(static state =>
            {
                var respHeaders = ((HttpResponse)state).Headers;
                if (string.IsNullOrEmpty(respHeaders.CacheControl))
                {
                    respHeaders.CacheControl = "no-store";
                    respHeaders.Pragma = "no-cache";
                }
                return Task.CompletedTask;
            }, response);
        }

        await next(context);
    }
}
