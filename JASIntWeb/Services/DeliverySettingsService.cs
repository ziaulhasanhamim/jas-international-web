using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

public sealed class DeliverySettingsService(IDbContextFactory<AppDbContext> dbFactory, HybridCache cache)
{
    public const string CacheKey = "Jas_DeliverySettings_Cache";
    public static ValueTask InvalidateCacheAsync(HybridCache cache) => cache.RemoveAsync(CacheKey);

    public async Task<DeliverySettings> GetSettingsAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(CacheKey, async token =>
        {
            await using var db = await dbFactory.CreateDbContextAsync(token);
            return await db.DeliverySettings.AsNoTracking().FirstOrDefaultAsync(token) ?? new DeliverySettings();
        }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMinutes(5) }, cancellationToken: ct);

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
