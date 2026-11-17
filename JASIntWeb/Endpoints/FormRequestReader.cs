using Microsoft.AspNetCore.Antiforgery;

namespace JASIntWeb.Endpoints;

internal union FormReadResult(IFormCollection, EndpointError);

internal static class FormRequestReader
{
    public static async Task<FormReadResult> ReadAsync(HttpContext context, IAntiforgery antiforgery, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasFormContentType) return new EndpointError("Form content required");

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return await context.Request.ReadFormAsync(ct);
        }
        catch (AntiforgeryValidationException)
        {
            return new EndpointError("Refresh the page and try again.");
        }
        catch (BadHttpRequestException)
        {
            return new EndpointError("The uploaded form is invalid or too large.");
        }
    }
}
