using System.Text.Json;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Services.Meta;
using Microsoft.AspNetCore.Http;

namespace JASIntWeb.Middleware;

public sealed class MetaAttributionMiddleware(RequestDelegate next)
{
    public const string AttributionCookieName = "jas_attribution";
    public const string FirstTouchCookieName = "jas_first_touch";
    public const string FbcCookieName = "_fbc";
    public const string FbpCookieName = "_fbp";
    public const string AttributionItemKey = "MetaAttribution";
    public const string ClientIpItemKey = "ClientIpAddress";
    public const string ClientUserAgentItemKey = "ClientUserAgent";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/feeds/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/feeds/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/sitemap", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.Request.Cookies["jas_cookie_consent"] == "essential")
        {
            await next(context);
            return;
        }
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

        // 4. Handle UTM & Meta dynamic campaign parameters
        string? utmSource = query["utm_source"].FirstOrDefault();
        string? utmMedium = query["utm_medium"].FirstOrDefault();
        string? utmCampaign = query["utm_campaign"].FirstOrDefault();
        string? utmContent = query["utm_content"].FirstOrDefault();
        string? utmTerm = query["utm_term"].FirstOrDefault();

        string? campaignId = query["campaign_id"].FirstOrDefault() ?? query["campaignid"].FirstOrDefault();
        string? adSetId = query["adset_id"].FirstOrDefault() ?? query["adsetid"].FirstOrDefault();
        string? adId = query["ad_id"].FirstOrDefault() ?? query["adid"].FirstOrDefault();

        var landingPath = request.Path.Value ?? "/";
        if (request.QueryString.HasValue)
        {
            landingPath += request.QueryString.Value;
        }

        var hasUtmInQuery = !string.IsNullOrWhiteSpace(utmSource) ||
                            !string.IsNullOrWhiteSpace(utmMedium) ||
                            !string.IsNullOrWhiteSpace(utmCampaign) ||
                            !string.IsNullOrWhiteSpace(utmContent) ||
                            !string.IsNullOrWhiteSpace(utmTerm) ||
                            !string.IsNullOrWhiteSpace(campaignId);

        // 5. Manage First-Touch Source
        string? firstTouchSource = null;
        if (request.Cookies.TryGetValue(FirstTouchCookieName, out var existingFirstTouch) && !string.IsNullOrWhiteSpace(existingFirstTouch))
        {
            firstTouchSource = existingFirstTouch;
        }
        else if (hasUtmInQuery || !string.IsNullOrWhiteSpace(fbclid))
        {
            firstTouchSource = !string.IsNullOrWhiteSpace(utmSource) ? utmSource : (!string.IsNullOrWhiteSpace(fbclid) ? "meta_ads" : "direct");
            try
            {
                response.Cookies.Append(FirstTouchCookieName, firstTouchSource, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(90),
                    Path = "/",
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = request.IsHttps
                });
            }
            catch
            {
                // Ignore cookie writing issues
            }
        }

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
                UtmTerm: utmTerm,
                CampaignId: campaignId,
                AdSetId: adSetId,
                AdId: adId,
                LandingPath: landingPath,
                FirstTouchSource: firstTouchSource,
                LastTouchSource: utmSource ?? "meta_ads"
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
                UtmTerm: stored?.UtmTerm,
                CampaignId: stored?.CampaignId ?? campaignId,
                AdSetId: stored?.AdSetId ?? adSetId,
                AdId: stored?.AdId ?? adId,
                LandingPath: stored?.LandingPath ?? landingPath,
                FirstTouchSource: firstTouchSource ?? stored?.FirstTouchSource,
                LastTouchSource: stored?.LastTouchSource ?? stored?.UtmSource
            );
        }

        context.Items[AttributionItemKey] = attribution;

        await next(context);
    }
}
