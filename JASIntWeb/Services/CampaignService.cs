using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

public sealed class CampaignService(
    IDbContextFactory<AppDbContext> dbFactory,
    HybridCache cache)
{
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

                // Collect product IDs: Curated first, then tagged
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

                var products = new List<ProductSummaryDto>();
                if (distinctProductIds.Count > 0)
                {
                    var query = db.Products.AsNoTracking()
                        .Where(p => distinctProductIds.Contains(p.Id) && !p.IsDeleted && p.ProductVariants.Any(v => v.IsActive && !v.IsDeleted))
                        .Select(p => new
                        {
                            p.Id,
                            p.Name,
                            p.Slug,
                            Price = p.ProductVariants
                                .Where(v => v.IsActive && !v.IsDeleted)
                                .OrderByDescending(v => v.IsDefault)
                                .ThenBy(v => v.Id)
                                .Select(v => v.SellingPrice)
                                .FirstOrDefault(),
                            CategoryName = p.Category != null ? p.Category.Name : "",
                            p.MainImageUrl,
                            DefaultVariantId = p.ProductVariants
                                .Where(v => v.IsDefault && v.IsActive && !v.IsDeleted)
                                .Select(v => (int?)v.Id)
                                .FirstOrDefault(),
                            AvailableStock = db.InventoryItems
                                .Where(i => i.ProductVariant!.ProductId == p.Id && i.ProductVariant.IsDefault && i.ProductVariant.IsActive && !i.ProductVariant.IsDeleted)
                                .Select(i => i.QuantityOnHand - i.QuantityReserved)
                                .FirstOrDefault(),
                            BrandName = p.Brand != null ? p.Brand.Name : null,
                            BrandSlug = p.Brand != null ? p.Brand.Slug : null,
                            CompareAtPrice = p.ProductVariants
                                .Where(v => v.IsDefault && v.IsActive && !v.IsDeleted)
                                .Select(v => v.CompareAtPrice)
                                .FirstOrDefault(),
                            Rating = db.ProductReviews.Where(r => r.ProductId == p.Id && r.IsApproved).Average(r => (decimal?)r.Rating) ?? 5.0m,
                            ReviewCount = db.ProductReviews.Count(r => r.ProductId == p.Id && r.IsApproved),
                            p.IsNewArrival,
                            p.IsFeatured,
                            HasVariants = p.Attributes.Any(),
                            Tags = p.ProductTags
                                .Where(pt => !pt.Tag.IsDeleted && !pt.Tag.IsHidden)
                                .OrderBy(pt => pt.Tag.SortOrder)
                                .Select(pt => new TagDto(pt.Tag.Id, pt.Tag.Name, pt.Tag.Slug, pt.Tag.IsHidden, pt.Tag.BadgeColor, pt.Tag.SortOrder, 0))
                                .ToList()
                        });

                    var rawProducts = await query.ToListAsync(token);
                    var productMap = rawProducts.ToDictionary(p => p.Id);

                    // Preserve curated ordering first
                    foreach (var id in distinctProductIds)
                    {
                        if (productMap.TryGetValue(id, out var p))
                        {
                            products.Add(new ProductSummaryDto(
                                p.Id, p.Name, p.Slug, p.Price, p.CategoryName, p.MainImageUrl,
                                p.DefaultVariantId, p.AvailableStock, p.BrandName, p.BrandSlug,
                                p.CompareAtPrice, p.Rating, p.ReviewCount, p.IsNewArrival,
                                p.IsFeatured, p.HasVariants)
                            {
                                Tags = p.Tags
                            });
                        }
                    }
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
            cancellationToken: ct);
    }

    public async ValueTask InvalidateCacheAsync(string? slug = null)
    {
        await cache.RemoveAsync(ActiveCampaignsCacheKey);
        if (!string.IsNullOrWhiteSpace(slug))
        {
            await cache.RemoveAsync($"campaign:{slug.Trim().ToLowerInvariant()}");
        }
    }
}
