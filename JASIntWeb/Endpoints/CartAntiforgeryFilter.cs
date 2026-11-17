using Microsoft.AspNetCore.Antiforgery;

namespace JASIntWeb.Endpoints;

public sealed class CartAntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            return await next(invocation);

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new ApiErrorDto("Refresh the page and try again.", "csrf"));
        }
        return await next(invocation);
    }
}
