using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JASIntWeb.Services.Meta;

public sealed partial class MetaConversionsApiService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    DeliverySettingsService deliverySettingsService,
    ILogger<MetaConversionsApiService> logger) : IMetaConversionsApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<MetaAdsConfig> GetEffectiveConfigAsync()
    {
        string? pixelId = null;
        string? accessToken = null;
        string? testEventCode = null;
        bool enabled = true;

        try
        {
            var dbSettings = await deliverySettingsService.GetSettingsAsync();
            pixelId = dbSettings.MetaPixelId;
            accessToken = dbSettings.MetaAccessToken;
            testEventCode = dbSettings.MetaTestEventCode;
            enabled = dbSettings.MetaTrackingEnabled;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not fetch Meta Ads settings from database, falling back to configuration.");
        }

        // Fallbacks to configuration / environment variables if not set in DB
        pixelId = string.IsNullOrWhiteSpace(pixelId)
            ? (config["Meta:PixelId"] ?? config["META_PIXEL_ID"])
            : pixelId;

        accessToken = string.IsNullOrWhiteSpace(accessToken)
            ? (config["Meta:AccessToken"] ?? config["META_ACCESS_TOKEN"])
            : accessToken;

        testEventCode = string.IsNullOrWhiteSpace(testEventCode)
            ? (config["Meta:TestEventCode"] ?? config["META_TEST_EVENT_CODE"])
            : testEventCode;

        var isConfigured = !string.IsNullOrWhiteSpace(pixelId) && !string.IsNullOrWhiteSpace(accessToken);

        return new MetaAdsConfig(pixelId?.Trim(), accessToken?.Trim(), testEventCode?.Trim(), enabled, isConfigured);
    }

    public async Task<bool> SendPurchaseEventAsync(Order order, CheckoutRequest? request = null, HttpContext? httpContext = null, CancellationToken ct = default)
    {
        try
        {
            var cfg = await GetEffectiveConfigAsync();
            if (!cfg.Enabled || !cfg.IsConfigured)
            {
                logger.LogDebug("Meta CAPI tracking is disabled or not configured; skipping Purchase event.");
                return false;
            }

            var eventId = order.PublicToken.ToString();
            var eventSourceUrl = httpContext != null
                ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/order/{order.PublicToken}"
                : $"https://jasinternational.com.bd/order/{order.PublicToken}";

            var customerName = order.DeliveryAddress?.CustomerName ?? request?.CustomerName ?? "";
            var (firstName, lastName) = SplitName(customerName);

            var email = order.DeliveryAddress?.Email ?? request?.Email;
            var phone = order.DeliveryAddress?.PhoneNumber ?? request?.PhoneNumber;
            var city = order.DeliveryAddress?.City ?? request?.City ?? order.DeliveryAddress?.District ?? request?.District;
            var state = order.DeliveryAddress?.Division ?? request?.Division;

            var clientIp = order.ClientIpAddress;
            var userAgent = order.ClientUserAgent;
            if (httpContext != null)
            {
                clientIp ??= ResolveClientIp(httpContext);
                userAgent ??= httpContext.Request.Headers.UserAgent.ToString();
            }

            var fbp = order.Fbp ?? request?.Fbp;
            var fbc = order.Fbc ?? request?.Fbc;

            var userData = new MetaUserData
            {
                Email = email,
                Phone = phone,
                FirstName = firstName,
                LastName = lastName,
                City = city,
                State = state,
                Country = "BD",
                ClientIpAddress = clientIp,
                ClientUserAgent = userAgent,
                Fbp = fbp,
                Fbc = fbc,
                ExternalId = order.UserId.HasValue ? order.UserId.Value.ToString() : null
            };

            var items = order.Items?.Select(i => new MetaContentItem
            {
                Id = i.ProductVariantId > 0 ? i.ProductVariantId.ToString() : i.ProductName,
                Title = $"{i.ProductName} ({i.VariantName})",
                Quantity = i.Quantity,
                ItemPrice = i.UnitPrice
            }).ToList() ?? [];

            var customData = new MetaCustomData
            {
                Value = order.Total,
                Currency = "BDT",
                ContentType = "product",
                OrderId = order.Id.ToString(),
                NumItems = order.Items?.Sum(i => i.Quantity) ?? 1,
                ContentIds = items.Select(i => i.Id).ToList(),
                Contents = items
            };

            return await SendEventAsync("Purchase", eventId, eventSourceUrl, userData, customData, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Meta CAPI Purchase event for Order #{OrderId}", order.Id);
            return false;
        }
    }

    public async Task<bool> SendEventAsync(string eventName, string eventId, string? eventSourceUrl, MetaUserData userData, MetaCustomData? customData = null, CancellationToken ct = default)
    {
        var cfg = await GetEffectiveConfigAsync();
        if (!cfg.Enabled || !cfg.IsConfigured)
        {
            return false;
        }

        var (success, _) = await ExecuteSendEventAsync(cfg.PixelId!, cfg.AccessToken!, cfg.TestEventCode, eventName, eventId, eventSourceUrl, userData, customData, ct);
        return success;
    }

    public async Task<(bool Success, string Message)> SendTestEventAsync(string pixelId, string accessToken, string? testEventCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pixelId) || string.IsNullOrWhiteSpace(accessToken))
        {
            return (false, "Pixel ID and Access Token are required.");
        }

        var dummyUserData = new MetaUserData
        {
            Email = "test@jasinternational.com.bd",
            Phone = "01711465078",
            FirstName = "Test",
            LastName = "Customer",
            City = "Khulna",
            Country = "BD",
            ClientIpAddress = "103.230.104.1",
            ClientUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36"
        };

        var dummyCustomData = new MetaCustomData
        {
            Value = 1500m,
            Currency = "BDT",
            ContentType = "product",
            ContentName = "Meta CAPI Test Verification Event",
            OrderId = $"TEST-{Random.Shared.Next(1000, 9999)}"
        };

        var eventId = $"test_{Guid.NewGuid():N}";
        return await ExecuteSendEventAsync(pixelId.Trim(), accessToken.Trim(), testEventCode?.Trim(), "TestEvent", eventId, "https://jasinternational.com.bd/admin/settings/meta-ads", dummyUserData, dummyCustomData, ct);
    }

    private async Task<(bool Success, string Message)> ExecuteSendEventAsync(
        string pixelId,
        string accessToken,
        string? testEventCode,
        string eventName,
        string eventId,
        string? eventSourceUrl,
        MetaUserData userData,
        MetaCustomData? customData,
        CancellationToken ct)
    {
        try
        {
            var eventPayload = new Dictionary<string, object?>
            {
                ["event_name"] = eventName,
                ["event_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["event_id"] = eventId,
                ["event_source_url"] = eventSourceUrl ?? "https://jasinternational.com.bd",
                ["action_source"] = "website",
                ["user_data"] = FormatUserData(userData)
            };

            if (customData != null)
            {
                eventPayload["custom_data"] = FormatCustomData(customData);
            }

            var requestBody = new Dictionary<string, object?>
            {
                ["data"] = new[] { eventPayload }
            };

            if (!string.IsNullOrWhiteSpace(testEventCode))
            {
                requestBody["test_event_code"] = testEventCode.Trim();
            }

            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            using var client = httpClientFactory.CreateClient("MetaCAPI");

            var url = $"https://graph.facebook.com/v19.0/{pixelId}/events?access_token={Uri.EscapeDataString(accessToken)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Meta CAPI event '{EventName}' sent successfully for EventId '{EventId}'. Response: {Response}", eventName, eventId, responseContent);
                return (true, $"Event received by Meta! ({response.StatusCode})");
            }
            else
            {
                logger.LogWarning("Meta CAPI event '{EventName}' failed with HTTP {Status}: {Response}", eventName, response.StatusCode, responseContent);
                return (false, $"Meta API error ({response.StatusCode}): {responseContent}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while sending Meta CAPI event '{EventName}'", eventName);
            return (false, $"Exception: {ex.Message}");
        }
    }

    private static Dictionary<string, object?> FormatUserData(MetaUserData user)
    {
        var dict = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(user.Email))
            dict["em"] = new[] { HashSha256(NormalizeEmail(user.Email)) };

        if (!string.IsNullOrWhiteSpace(user.Phone))
            dict["ph"] = new[] { HashSha256(NormalizePhone(user.Phone)) };

        if (!string.IsNullOrWhiteSpace(user.FirstName))
            dict["fn"] = new[] { HashSha256(user.FirstName.Trim().ToLowerInvariant()) };

        if (!string.IsNullOrWhiteSpace(user.LastName))
            dict["ln"] = new[] { HashSha256(user.LastName.Trim().ToLowerInvariant()) };

        if (!string.IsNullOrWhiteSpace(user.City))
            dict["ct"] = new[] { HashSha256(user.City.Trim().ToLowerInvariant()) };

        if (!string.IsNullOrWhiteSpace(user.State))
            dict["st"] = new[] { HashSha256(user.State.Trim().ToLowerInvariant()) };

        if (!string.IsNullOrWhiteSpace(user.Country))
            dict["country"] = new[] { HashSha256(user.Country.Trim().ToLowerInvariant()) };

        if (!string.IsNullOrWhiteSpace(user.ClientIpAddress))
            dict["client_ip_address"] = user.ClientIpAddress.Trim();

        if (!string.IsNullOrWhiteSpace(user.ClientUserAgent))
            dict["client_user_agent"] = user.ClientUserAgent.Trim();

        if (!string.IsNullOrWhiteSpace(user.Fbp))
            dict["fbp"] = user.Fbp.Trim();

        if (!string.IsNullOrWhiteSpace(user.Fbc))
            dict["fbc"] = user.Fbc.Trim();

        if (!string.IsNullOrWhiteSpace(user.ExternalId))
            dict["external_id"] = new[] { HashSha256(user.ExternalId.Trim()) };

        return dict;
    }

    private static Dictionary<string, object?> FormatCustomData(MetaCustomData data)
    {
        var dict = new Dictionary<string, object?>
        {
            ["currency"] = data.Currency,
            ["value"] = data.Value
        };

        if (!string.IsNullOrWhiteSpace(data.ContentType))
            dict["content_type"] = data.ContentType;

        if (!string.IsNullOrWhiteSpace(data.ContentName))
            dict["content_name"] = data.ContentName;

        if (!string.IsNullOrWhiteSpace(data.ContentCategory))
            dict["content_category"] = data.ContentCategory;

        if (data.ContentIds is { Count: > 0 })
            dict["content_ids"] = data.ContentIds;

        if (data.NumItems.HasValue)
            dict["num_items"] = data.NumItems.Value;

        if (!string.IsNullOrWhiteSpace(data.OrderId))
            dict["order_id"] = data.OrderId;

        if (data.Contents is { Count: > 0 })
        {
            dict["contents"] = data.Contents.Select(c => new Dictionary<string, object?>
            {
                ["id"] = c.Id,
                ["quantity"] = c.Quantity,
                ["item_price"] = c.ItemPrice,
                ["title"] = c.Title
            }).ToList();
        }

        return dict;
    }

    public static string HashSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    public static string NormalizePhone(string phone)
    {
        var digits = NonDigitsRegex().Replace(phone, "");
        if (digits.StartsWith("880") && digits.Length >= 13)
        {
            return digits;
        }
        if (digits.StartsWith("01") && digits.Length == 11)
        {
            return "88" + digits;
        }
        return digits;
    }

    public static (string FirstName, string LastName) SplitName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return ("", "");
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return (parts[0], "");
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    public static string? ResolveClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitsRegex();
}
