using JASIntWeb.Services;

namespace JASIntWeb.Endpoints;

public sealed class CartRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/cart"))
        {
            await next(context);
            return;
        }

        context.Response.OnStarting(static state =>
        {
            var headers = ((HttpResponse)state).Headers;
            if (string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-store";
                headers.Pragma = "no-cache";
            }
            return Task.CompletedTask;
        }, context.Response);
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status400BadRequest)
        {
            await Results.BadRequest(new ApiErrorDto("Review the submitted values and try again.", "validation"))
                .ExecuteAsync(context);
        }
        catch (ShopException error)
        {
            await Results.Conflict(new ApiErrorDto(error.Message, null)).ExecuteAsync(context);
        }
    }
}
