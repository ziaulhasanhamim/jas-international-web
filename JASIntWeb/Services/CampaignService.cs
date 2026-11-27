using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

public sealed class CampaignService(
    IDbContextFactory<AppDbContext> dbFactory,
    HybridCache cache,
    CollectionService collectionService,
    CampaignPricingService? campaignPricing = null)
{
    public const string TagCampaigns = "campaigns";
    private const string ActiveCampaignsCacheKey = "campaigns:active";
    private static readonly HybridCacheEntryOptions StandardOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public async Task<List<CampaignSummaryDto>> GetActiveCampaignsAsync(CancellationToken ct = default)
    {
        var list = await cache.GetOrCreateAsync(
            ActiveCampaignsCacheKey,
            async token =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(token);
                var campaigns = await db.Campaigns.AsNoTracking()
                    .Include(c => c.CampaignProducts)
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ThenByDescending(c => c.CreatedAt)
                    .ToListAsync(token);

                var summaries = new List<CampaignSummaryDto>(campaigns.Count);
                foreach (var c in campaigns)
                {
                    var distinctProductIds = new HashSet<int>(c.CampaignProducts.Select(cp => cp.ProductId));
                    if (c.CollectionId.HasValue)
                    {
                        var ids = await collectionService.GetCollectionProductIdsAsync(c.CollectionId.Value, token);
                        foreach (var id in ids) distinctProductIds.Add(id);
                    }
                    else
                    {
                        if (c.TagId.HasValue)
                        {
                            var taggedIds = await db.ProductTags.AsNoTracking()
                                .Where(pt => pt.TagId == c.TagId.Value && !pt.Product.IsDeleted && pt.Product.ProductVariants.Any(v => v.IsActive && !v.IsDeleted))
                                .Select(pt => pt.ProductId)
                                .ToListAsync(token);
                            foreach (var id in taggedIds) distinctProductIds.Add(id);
                        }
                        foreach (var id in c.CuratedProductIds) distinctProductIds.Add(id);
                    }

                    summaries.Add(new CampaignSummaryDto(
                        c.Id,
                        c.Name,
                        c.Slug,
                        c.BanglaName,
                        c.Eyebrow,
                        c.Description,
                        c.BannerImageUrl ?? "",
                        c.MobileBannerImageUrl,
                        c.StartDate,
                        c.EndDate,
                        c.ShowCountdown,
                        c.ThemeColor,
                        c.HighlightCouponCode,
                        distinctProductIds.Count,
                        c.IsActive,
                        c.SortOrder,
                        c.DiscountScenario,
                        c.BadgeText,
                        c.DefaultDiscountType,
                        c.DefaultDiscountValue));
                }

                return summaries;
            },
            StandardOptions,
            tags: [TagCampaigns, CollectionService.TagCollections, CatalogService.TagCatalog],
            cancellationToken: ct);

        var now = DateTimeOffset.UtcNow;
        return list.Where(c => !c.EndDate.HasValue || c.EndDate.Value > now).ToList();
    }

    public async Task<CampaignDetailsDto?> GetCampaignBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var cacheKey = $"campaign:{normalizedSlug}";

        var details = await cache.GetOrCreateAsync(
            cacheKey,
            async token =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(token);
                var campaign = await db.Campaigns.AsNoTracking()
                    .Include(c => c.CampaignProducts)
                    .FirstOrDefaultAsync(c => c.Slug == normalizedSlug && c.IsActive, token);

                if (campaign == null) return null;

                var orderedIds = new List<int>();
                var seen = new HashSet<int>();

                // Explicit campaign product overrides take showcase priority in SortOrder
                foreach (var cp in campaign.CampaignProducts.OrderBy(cp => cp.SortOrder).ThenBy(cp => cp.Id))
                {
                    if (seen.Add(cp.ProductId))
                    {
                        orderedIds.Add(cp.ProductId);
                    }
                }

                if (campaign.CollectionId.HasValue)
                {
                    var colIds = await collectionService.GetCollectionProductIdsAsync(campaign.CollectionId.Value, token);
                    foreach (var id in colIds)
                    {
                        if (seen.Add(id)) orderedIds.Add(id);
                    }
                }
                else
                {
                    foreach (var id in campaign.CuratedProductIds)
                    {
                        if (seen.Add(id)) orderedIds.Add(id);
                    }

                    if (campaign.TagId.HasValue)
                    {
                        var taggedProductIds = await db.ProductTags.AsNoTracking()
                            .Where(pt => pt.TagId == campaign.TagId.Value && !pt.Product.IsDeleted && pt.Product.ProductVariants.Any(v => v.IsActive && !v.IsDeleted))
                            .Select(pt => pt.ProductId)
                            .ToListAsync(token);

                        foreach (var id in taggedProductIds)
                        {
                            if (seen.Add(id)) orderedIds.Add(id);
                        }
                    }
                }

                var products = orderedIds.Count > 0
                    ? await CollectionService.LoadOrderedProductSummariesAsync(db, orderedIds, token)
                    : [];

                return new CampaignDetailsDto(
                    campaign.Id,
                    campaign.Name,
                    campaign.Slug,
                    campaign.BanglaName,
                    campaign.Eyebrow,
                    campaign.Description,
                    campaign.BannerImageUrl,
                    campaign.MobileBannerImageUrl,
                    campaign.StartDate,
                    campaign.EndDate,
                    campaign.ShowCountdown,
                    campaign.ThemeColor,
                    campaign.HighlightCouponCode,
                    campaign.MetaTitle,
                    campaign.MetaDescription,
                    campaign.OgImageUrl,
                    products,
                    campaign.DiscountScenario,
                    campaign.BadgeText,
                    campaign.DefaultDiscountType,
                    campaign.DefaultDiscountValue);
            },
            StandardOptions,
            tags: [TagCampaigns, CollectionService.TagCollections, CatalogService.TagCatalog],
            cancellationToken: ct);

        if (details is not null && campaignPricing is not null && details.Products.Count > 0)
        {
            var pricedProducts = await campaignPricing.ApplyCampaignPricingAsync(details.Products, ct);
            var now = DateTimeOffset.UtcNow;
            if (details.StartDate.HasValue && details.StartDate.Value > now
                && (details.DiscountScenario == CampaignDiscountScenario.DirectDiscount
                    || details.DiscountScenario == CampaignDiscountScenario.DirectAndCoupon))
            {
                pricedProducts = await campaignPricing.PreviewUpcomingCampaignPricingAsync(details.Id, pricedProducts, ct);
            }
            return details with { Products = pricedProducts };
        }

        return details;
    }

    public async ValueTask InvalidateCacheAsync(string? slug = null)
    {
        campaignPricing?.InvalidateCache();
        await cache.RemoveAsync(ActiveCampaignsCacheKey);
        await cache.RemoveByTagAsync(TagCampaigns);
        if (!string.IsNullOrWhiteSpace(slug))
        {
            await cache.RemoveAsync($"campaign:{slug.Trim().ToLowerInvariant()}");
        }
    }
}
