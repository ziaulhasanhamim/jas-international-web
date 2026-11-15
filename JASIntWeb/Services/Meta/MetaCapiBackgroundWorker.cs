using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JASIntWeb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JASIntWeb.Services.Meta;

public sealed class MetaCapiBackgroundWorker(
    IMetaCapiQueue queue,
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<MetaCapiBackgroundWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Meta CAPI Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = new List<MetaCapiEventPayload>();

                // Wait for the first item
                await foreach (var item in queue.ReadAllAsync(stoppingToken))
                {
                    batch.Add(item);

                    // Pull any additional available items up to 10
                    while (batch.Count < 10 && queue.PendingCount > 0)
                    {
                        // We will drain what's immediately available
                        break;
                    }

                    if (batch.Count >= 10)
                    {
                        break;
                    }

                    // Brief wait to allow batching if events arrive together
                    if (batch.Count > 0 && queue.PendingCount == 0)
                    {
                        break;
                    }
                }

                if (batch.Count > 0)
                {
                    await ProcessBatchWithRetryAsync(batch, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in Meta CAPI Background Worker loop.");
                await Task.Delay(2000, stoppingToken);
            }
        }

        logger.LogInformation("Meta CAPI Background Worker stopping.");
    }

    private async Task ProcessBatchWithRetryAsync(List<MetaCapiEventPayload> batch, CancellationToken ct)
    {
        var (pixelId, accessToken, testEventCode, enabled) = await ResolveMetaConfigAsync();

        if (!enabled || string.IsNullOrWhiteSpace(pixelId) || string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogDebug("Meta CAPI is disabled or not configured; dropped {Count} events.", batch.Count);
            return;
        }

        var formattedEvents = batch.Select(payload =>
        {
            var dict = new Dictionary<string, object?>
            {
                ["event_name"] = payload.EventName,
                ["event_time"] = payload.EventTime,
                ["event_id"] = payload.EventId,
                ["event_source_url"] = payload.EventSourceUrl ?? "https://www.jasinternational.com.bd",
                ["action_source"] = "website",
                ["user_data"] = MetaConversionsApiService.FormatUserDataInternal(payload.UserData)
            };

            if (payload.CustomData != null)
            {
                dict["custom_data"] = MetaConversionsApiService.FormatCustomDataInternal(payload.CustomData);
            }

            return dict;
        }).ToList();

        var requestBody = new Dictionary<string, object?>
        {
            ["data"] = formattedEvents
        };

        if (!string.IsNullOrWhiteSpace(testEventCode))
        {
            requestBody["test_event_code"] = testEventCode.Trim();
        }

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);

        var success = false;
        var maxAttempts = 3;
        var attempt = 0;

        while (!success && attempt < maxAttempts && !ct.IsCancellationRequested)
        {
            attempt++;
            try
            {
                using var client = httpClientFactory.CreateClient("MetaCAPI");
                var url = $"https://graph.facebook.com/v19.0/{pixelId}/events?access_token={Uri.EscapeDataString(accessToken)}";
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var response = await client.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Meta CAPI sent {Count} events successfully (attempt {Attempt}). Response: {Response}", batch.Count, attempt, body);
                    success = true;
                }
                else
                {
                    logger.LogWarning("Meta CAPI HTTP {StatusCode} on attempt {Attempt}: {Response}", (int)response.StatusCode, attempt, body);
                    // If client error (4xx) other than rate limit (429), retrying the same payload won't help
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500 && (int)response.StatusCode != 429)
                    {
                        break;
                    }
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(1000 * (int)Math.Pow(2, attempt), ct);
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Meta CAPI network error on attempt {Attempt}/{Max}", attempt, maxAttempts);
                if (attempt < maxAttempts)
                {
                    await Task.Delay(1000 * (int)Math.Pow(2, attempt), ct);
                }
            }
        }
    }

    private async Task<(string? PixelId, string? AccessToken, string? TestEventCode, bool Enabled)> ResolveMetaConfigAsync()
    {
        string? pixelId = null;
        string? accessToken = null;
        string? testEventCode = null;
        var enabled = true;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var deliverySettingsService = scope.ServiceProvider.GetRequiredService<DeliverySettingsService>();
            var dbSettings = await deliverySettingsService.GetSettingsAsync();
            pixelId = dbSettings.MetaPixelId;
            accessToken = dbSettings.MetaAccessToken;
            testEventCode = dbSettings.MetaTestEventCode;
            enabled = dbSettings.MetaTrackingEnabled;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not fetch Meta config from database; falling back to configuration.");
        }

        pixelId = string.IsNullOrWhiteSpace(pixelId)
            ? (config["Meta:PixelId"] ?? config["META_PIXEL_ID"])
            : pixelId;

        accessToken = string.IsNullOrWhiteSpace(accessToken)
            ? (config["Meta:AccessToken"] ?? config["META_ACCESS_TOKEN"])
            : accessToken;

        testEventCode = string.IsNullOrWhiteSpace(testEventCode)
            ? (config["Meta:TestEventCode"] ?? config["META_TEST_EVENT_CODE"])
            : testEventCode;

        return (pixelId?.Trim(), accessToken?.Trim(), testEventCode?.Trim(), enabled);
    }
}
