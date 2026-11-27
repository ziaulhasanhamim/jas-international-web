using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

public sealed class CampaignService(
    IDbContextFactory<AppDbContext> dbFactory,
    HybridCache cache,
    CollectionService collectionService)
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
        return await cache.GetOrCreateAsync(
            ActiveCampaignsCacheKey,
            async token =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(token);
                var now = DateTimeOffset.UtcNow;
                var campaigns = await db.Campaigns.AsNoTracking()
                    .Where(c => c.IsActive && (!c.EndDate.HasValue || c.EndDate > now))
                    .OrderBy(c => c.SortOrder)
                    .ThenByDescending(c => c.CreatedAt)
                    .ToListAsync(token);

                var summaries = new List<CampaignSummaryDto>(campaigns.Count);
                foreach (var c in campaigns)
                {
                    int productCount = 0;
                    if (c.CollectionId.HasValue)
                    {
                        var ids = await collectionService.GetCollectionProductIdsAsync(c.CollectionId.Value, token);
                        productCount = ids.Count;
                    }
                    else
                    {
                        if (c.TagId.HasValue)
                        {
                            productCount += await db.ProductTags
                                .CountAsync(pt => pt.TagId == c.TagId.Value && !pt.Product.IsDeleted && pt.Product.ProductVariants.Any(v => v.IsActive && !v.IsDeleted), token);
                        }
                        if (c.CuratedProductIds.Count > 0)
                        {
                            var curatedDistinct = c.CuratedProductIds.Distinct().Count();
                            productCount = Math.Max(productCount, curatedDistinct);
                        }
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
                        productCount,
                        c.IsActive,
                        c.SortOrder));
                }

                return summaries;
            },
            StandardOptions,
            tags: [TagCampaigns, CollectionService.TagCollections, CatalogService.TagCatalog],
            cancellationToken: ct);
    }

    public async Task<CampaignDetailsDto?> GetCampaignBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var cacheKey = $"campaign:{normalizedSlug}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            async token =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(token);
                var campaign = await db.Campaigns.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Slug == normalizedSlug && c.IsActive, token);

                if (campaign == null) return null;

                List<ProductSummaryDto> products;
                if (campaign.CollectionId.HasValue)
                {
                    products = await collectionService.GetCollectionProductsAsync(campaign.CollectionId.Value, ct: token);
                }
                else
                {
                    var productIds = new List<int>();
                    if (campaign.CuratedProductIds.Count > 0)
                    {
                        productIds.AddRange(campaign.CuratedProductIds);
                    }

                    if (campaign.TagId.HasValue)
                    {
                        var taggedProductIds = await db.ProductTags.AsNoTracking()
                            .Where(pt => pt.TagId == campaign.TagId.Value && !pt.Product.IsDeleted && pt.Product.ProductVariants.Any(v => v.IsActive && !v.IsDeleted))
                            .Select(pt => pt.ProductId)
                            .ToListAsync(token);

                        productIds.AddRange(taggedProductIds);
                    }

                    var distinctProductIds = productIds.Distinct().ToList();
                    products = distinctProductIds.Count > 0
                        ? await CollectionService.LoadOrderedProductSummariesAsync(db, distinctProductIds, token)
                        : [];
                }

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
                    products);
            },
            StandardOptions,
            tags: [TagCampaigns, CollectionService.TagCollections, CatalogService.TagCatalog],
            cancellationToken: ct);
    }

    public async ValueTask InvalidateCacheAsync(string? slug = null)
    {
        await cache.RemoveAsync(ActiveCampaignsCacheKey);
        await cache.RemoveByTagAsync(TagCampaigns);
        if (!string.IsNullOrWhiteSpace(slug))
        {
            await cache.RemoveAsync($"campaign:{slug.Trim().ToLowerInvariant()}");
        }
    }
}
