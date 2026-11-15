using System;
using System.Threading.Tasks;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace JASIntWeb.Services;

public sealed class DeliverySettingsService(AppDbContext db, IMemoryCache? cache = null)
{
    private const string CacheKey = "Jas_DeliverySettings_Cache";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public static void InvalidateCache(IMemoryCache? cache)
    {
        cache?.Remove(CacheKey);
    }

    public DeliverySettings GetDeliverySettings()
    {
        if (cache is not null && cache.TryGetValue(CacheKey, out DeliverySettings? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var settings = db.DeliverySettings.AsNoTracking().FirstOrDefault() ?? new DeliverySettings();
            cache?.Set(CacheKey, settings, CacheDuration);
            return settings;
        }
        catch
        {
            return new DeliverySettings();
        }
    }

    public async Task<DeliverySettings> GetSettingsAsync()
    {
        if (cache is not null && cache.TryGetValue(CacheKey, out DeliverySettings? cached) && cached is not null)
        {
            return cached;
        }

        var settings = await db.DeliverySettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new DeliverySettings
            {
                HubDistrict = "Khulna",
                HubCity = "Khulna Sadar",
                HubCityFee = 50m,
                OutsideFee = 150m,
                FreeDeliveryThreshold = null,
                RequireAdvanceForCOD = true,
                AdvanceAmount = 150m,
                BkashNumber = "01711465078",
                NagadNumber = "01711465078",
                BanglaQrImageUrl = "/jas-logo.png",
                PaymentInstructions = "Send ৳150 Advance Delivery Fee to our bKash/Nagad number or scan Bangla QR with the Order Reference code to instantly confirm your order.",
                SmsWebhookSecret = "jas_secret_sms_2026",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.DeliverySettings.Add(settings);
            await db.SaveChangesAsync();
        }
        else if (settings.FreeDeliveryThreshold == 5000m)
        {
            settings.FreeDeliveryThreshold = null;
            await db.SaveChangesAsync();
        }

        cache?.Set(CacheKey, settings, CacheDuration);
        return settings;
    }

    public async Task<decimal> CalculateDeliveryChargeAsync(string? district, decimal subtotal)
    {
        var settings = await GetSettingsAsync();

        if (settings.FreeDeliveryThreshold.HasValue && settings.FreeDeliveryThreshold.Value > 0 && subtotal >= settings.FreeDeliveryThreshold.Value)
        {
            return 0m;
        }

        if (!string.IsNullOrWhiteSpace(district) &&
            string.Equals(district.Trim(), settings.HubDistrict.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return settings.HubCityFee;
        }

        return settings.OutsideFee;
    }
}
