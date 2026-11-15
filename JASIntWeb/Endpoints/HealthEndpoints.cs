using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JASIntWeb.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var options = new HealthCheckOptions
        {
            AllowCachingResponses = false,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

                // For HEAD requests (such as uptime monitors), return status code and headers without body
                if (HttpMethods.IsHead(context.Request.Method))
                {
                    return;
                }

                var response = new
                {
                    status = report.Status.ToString().ToLowerInvariant(),
                    timestamp = DateTimeOffset.UtcNow,
                    totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
                    entries = report.Entries.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            status = kvp.Value.Status.ToString().ToLowerInvariant(),
                            durationMs = Math.Round(kvp.Value.Duration.TotalMilliseconds, 2),
                            description = kvp.Value.Description,
                            error = kvp.Value.Exception?.Message
                        })
                };

                await JsonSerializer.SerializeAsync(context.Response.Body, response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
            }
        };

        // Built-in .NET Health Check endpoints supporting GET and HEAD
        endpoints.MapHealthChecks("/health", options).AllowAnonymous();
        endpoints.MapHealthChecks("/healthz", options).AllowAnonymous();

        return endpoints;
    }
}
