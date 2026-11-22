using System;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Services.Analytics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JASIntWeb.Endpoints;

public sealed class AnalyticsPayload
{
    public string? Vid { get; set; }
    public string? Sid { get; set; }
    public string? Path { get; set; }
    public string? Title { get; set; }
    public string? Referrer { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? Fbclid { get; set; }
    public int Duration { get; set; }
    public bool IsUnload { get; set; }
}

public static class AnalyticsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    extension (IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapAnalyticsEndpoints()
        {
        var group = endpoints.MapGroup("/api/analytics");

        group.MapPost("/collect", async (
            HttpContext context,
            IVisitorAnalyticsQueue queue) =>
        {
            var userAgent = context.Request.Headers.UserAgent.ToString();

            // 1. Fast Bot & Crawler Filter (Googlebot, Bing, Meta crawler, uptime bots, etc.)
            if (UserAgentParser.IsBot(userAgent))
            {
                return Results.NoContent();
            }

            // 2. Parse payload safely (supports both application/json and sendBeacon text/plain)
            AnalyticsPayload? payload = null;
            if (context.Request.HasJsonContentType())
            {
                try
                {
                    payload = await context.Request.ReadFromJsonAsync<AnalyticsPayload>(JsonOptions);
                }
                catch
                {
                    // Ignore malformed body
                }
            }
            else
            {
                try
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var body = await reader.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        payload = JsonSerializer.Deserialize<AnalyticsPayload>(body, JsonOptions);
                    }
                }
                catch
                {
                    // Ignore malformed body
                }
            }

            if (payload is null)
            {
                return Results.NoContent();
            }

            // 3. Ignore non-storefront paths if posted accidentally (e.g. admin or api)
            var path = string.IsNullOrWhiteSpace(payload.Path) ? "/" : payload.Path.Trim();
            if (path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                return Results.NoContent();
            }

            // 4. Client IP & Anonymization
            var rawIp = GetClientIp(context);
            var anonymizedIp = UserAgentParser.AnonymizeIp(rawIp);

            // 5. Geo Headers (Cloudflare / Reverse Proxy)
            string? city = null;
            if (context.Request.Headers.TryGetValue("CF-IPCity", out var cfCity) && !string.IsNullOrWhiteSpace(cfCity))
            {
                city = cfCity.ToString().Trim();
            }

            string? country = null;
            if (context.Request.Headers.TryGetValue("CF-IPCountry", out var cfCountry) && !string.IsNullOrWhiteSpace(cfCountry))
            {
                country = cfCountry.ToString().Trim();
            }

            // 6. Device & User Agent Parsing
            var (deviceType, browser, os) = UserAgentParser.Parse(userAgent);
            var referrerDomain = UserAgentParser.ExtractDomain(payload.Referrer);

            int? userId = null;
            if (context.User.Identity?.IsAuthenticated == true)
            {
                userId = ShoppingService.UserId(context.User);
            }

            var log = new VisitorLog
            {
                VisitorId = string.IsNullOrWhiteSpace(payload.Vid) ? Guid.NewGuid().ToString("N") : payload.Vid.Trim(),
                SessionId = string.IsNullOrWhiteSpace(payload.Sid) ? Guid.NewGuid().ToString("N") : payload.Sid.Trim(),
                UserId = userId,
                Path = path,
                Title = string.IsNullOrWhiteSpace(payload.Title) ? null : (payload.Title.Length > 200 ? payload.Title[..200] : payload.Title.Trim()),
                Referrer = string.IsNullOrWhiteSpace(payload.Referrer) ? null : (payload.Referrer.Length > 500 ? payload.Referrer[..500] : payload.Referrer.Trim()),
                ReferrerDomain = referrerDomain,
                IpAddress = anonymizedIp,
                City = city,
                Country = country,
                DeviceType = deviceType,
                Browser = browser,
                OperatingSystem = os,
                UserAgent = userAgent.Length > 500 ? userAgent[..500] : userAgent,
                UtmSource = string.IsNullOrWhiteSpace(payload.UtmSource) ? null : payload.UtmSource.Trim(),
                UtmMedium = string.IsNullOrWhiteSpace(payload.UtmMedium) ? null : payload.UtmMedium.Trim(),
                UtmCampaign = string.IsNullOrWhiteSpace(payload.UtmCampaign) ? null : payload.UtmCampaign.Trim(),
                Fbclid = string.IsNullOrWhiteSpace(payload.Fbclid) ? null : payload.Fbclid.Trim(),
                DurationSeconds = Math.Clamp(payload.Duration, 0, 86400),
                Timestamp = DateTimeOffset.UtcNow,
                IsBounce = true
            };

            await queue.EnqueueAsync(log);

            return Results.NoContent();
        }).DisableAntiforgery();

        return endpoints;
    }
    }

    private static string GetClientIp(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfIp) && !string.IsNullOrWhiteSpace(cfIp))
        {
            return cfIp.ToString().Trim();
        }

        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var fwdIp) && !string.IsNullOrWhiteSpace(fwdIp))
        {
            var first = fwdIp.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Real-IP", out var realIp) && !string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.ToString().Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}
