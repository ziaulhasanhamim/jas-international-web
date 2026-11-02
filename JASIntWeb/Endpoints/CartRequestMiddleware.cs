using JASIntWeb.Services;
using Microsoft.AspNetCore.Antiforgery;

namespace JASIntWeb.Endpoints;

public sealed class CartRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery csrf)
    {
        if (!context.Request.Path.StartsWithSegments("/api/cart"))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                await csrf.ValidateRequestAsync(context);
            }

            await next(context);
        }
        catch (AntiforgeryValidationException)
        {
            await Results.BadRequest(new ApiErrorDto("Your session expired. Try again.", "antiforgery"))
                .ExecuteAsync(context);
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
