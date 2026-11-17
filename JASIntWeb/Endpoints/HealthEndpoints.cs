using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JASIntWeb.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var ready = new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready"), ResponseWriter = WriteResponseAsync };
        var live = new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteResponseAsync };
        endpoints.MapHealthChecks("/health", ready).AllowAnonymous();
        endpoints.MapHealthChecks("/healthz", ready).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", ready).AllowAnonymous();
        endpoints.MapHealthChecks("/health/live", live).AllowAnonymous();
        return endpoints;
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsHead(context.Request.Method)) return Task.CompletedTask;

        // Public probes expose status, never connection strings or exception details.
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            timestamp = DateTimeOffset.UtcNow,
            entries = report.Entries.ToDictionary(entry => entry.Key,
                entry => new { status = entry.Value.Status.ToString().ToLowerInvariant() })
        }, context.RequestAborted);
    }
}
