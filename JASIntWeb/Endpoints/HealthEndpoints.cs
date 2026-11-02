namespace JASIntWeb.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", Get);
        return endpoints;
    }

    public static async Task<IResult> Get(HttpContext context, AppDbContext db, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        return await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "healthy" })
            : Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
