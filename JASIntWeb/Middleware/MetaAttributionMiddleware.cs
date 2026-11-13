using System.Text.Json;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Services.Meta;
using Microsoft.AspNetCore.Http;

namespace JASIntWeb.Middleware;

public sealed class MetaAttributionMiddleware(RequestDelegate next)
{
    public const string AttributionCookieName = "jas_attribution";
    public const string FbcCookieName = "_fbc";
    public const string FbpCookieName = "_fbp";
    public const string AttributionItemKey = "MetaAttribution";
    public const string ClientIpItemKey = "ClientIpAddress";
    public const string ClientUserAgentItemKey = "ClientUserAgent";

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var query = request.Query;

        var clientIp = MetaConversionsApiService.ResolveClientIp(context);
        var userAgent = request.Headers.UserAgent.ToString();

        context.Items[ClientIpItemKey] = clientIp;
        context.Items[ClientUserAgentItemKey] = userAgent;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 1. Handle fbclid -> _fbc cookie
        string? fbclid = null;
        if (query.TryGetValue("fbclid", out var fbclidVal) && !string.IsNullOrWhiteSpace(fbclidVal))
        {
            fbclid = fbclidVal.ToString().Trim();
            var fbcValue = $"fb.1.{nowMs}.{fbclid}";
            response.Cookies.Append(FbcCookieName, fbcValue, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(90),
                Path = "/",
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = request.IsHttps
            });
        }

        // 2. Handle _fbp cookie (ensure first-party browser ID exists)
        string? fbp = null;
        if (request.Cookies.TryGetValue(FbpCookieName, out var existingFbp) && !string.IsNullOrWhiteSpace(existingFbp))
        {
            fbp = existingFbp;
        }
        else
        {
            var randomSubId = Random.Shared.Next(100000000, 999999999);
            fbp = $"fb.1.{nowMs}.{randomSubId}";
            response.Cookies.Append(FbpCookieName, fbp, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(90),
                Path = "/",
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = request.IsHttps
            });
        }

        // 3. Handle _fbc fallback from existing cookie if not in query
        string? fbc = null;
        if (!string.IsNullOrEmpty(fbclid))
        {
            fbc = $"fb.1.{nowMs}.{fbclid}";
        }
        else if (request.Cookies.TryGetValue(FbcCookieName, out var existingFbc) && !string.IsNullOrWhiteSpace(existingFbc))
        {
            fbc = existingFbc;
            var parts = existingFbc.Split('.');
            if (parts.Length >= 4)
            {
                fbclid = parts[3];
            }
        }

        // 4. Handle UTM parameters
        string? utmSource = query["utm_source"].FirstOrDefault();
        string? utmMedium = query["utm_medium"].FirstOrDefault();
        string? utmCampaign = query["utm_campaign"].FirstOrDefault();
        string? utmContent = query["utm_content"].FirstOrDefault();
        string? utmTerm = query["utm_term"].FirstOrDefault();

        var hasUtmInQuery = !string.IsNullOrWhiteSpace(utmSource) ||
                            !string.IsNullOrWhiteSpace(utmMedium) ||
                            !string.IsNullOrWhiteSpace(utmCampaign) ||
                            !string.IsNullOrWhiteSpace(utmContent) ||
                            !string.IsNullOrWhiteSpace(utmTerm);

        MetaAttributionDto attribution;

        if (hasUtmInQuery || !string.IsNullOrWhiteSpace(fbclid))
        {
            // If from Meta ad but utm_source is omitted, infer Facebook/Instagram
            if (string.IsNullOrWhiteSpace(utmSource) && !string.IsNullOrWhiteSpace(fbclid))
            {
                utmSource = "meta_ads";
                utmMedium ??= "paid_social";
            }

            attribution = new MetaAttributionDto(
                Fbclid: fbclid,
                Fbp: fbp,
                Fbc: fbc,
                UtmSource: utmSource,
                UtmMedium: utmMedium,
                UtmCampaign: utmCampaign,
                UtmContent: utmContent,
                UtmTerm: utmTerm
            );

            // Save attribution to cookie for 30 days
            try
            {
                var cookieJson = JsonSerializer.Serialize(attribution);
                response.Cookies.Append(AttributionCookieName, cookieJson, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    Path = "/",
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = request.IsHttps
                });
            }
            catch
            {
                // Ignore cookie serialization issues
            }
        }
        else
        {
            // Try reading from stored attribution cookie
            MetaAttributionDto? stored = null;
            if (request.Cookies.TryGetValue(AttributionCookieName, out var storedJson) && !string.IsNullOrWhiteSpace(storedJson))
            {
                try
                {
                    stored = JsonSerializer.Deserialize<MetaAttributionDto>(storedJson);
                }
                catch
                {
                    stored = null;
                }
            }

            attribution = new MetaAttributionDto(
                Fbclid: stored?.Fbclid ?? fbclid,
                Fbp: fbp ?? stored?.Fbp,
                Fbc: fbc ?? stored?.Fbc,
                UtmSource: stored?.UtmSource,
                UtmMedium: stored?.UtmMedium,
                UtmCampaign: stored?.UtmCampaign,
                UtmContent: stored?.UtmContent,
                UtmTerm: stored?.UtmTerm
            );
        }

        context.Items[AttributionItemKey] = attribution;

        await next(context);
    }
}
