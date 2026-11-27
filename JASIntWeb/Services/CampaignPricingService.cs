using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace JASIntWeb.Services;

/// <summary>
/// Authoritative pricing engine for scheduled Campaigns and per-product/variant CampaignProduct overrides.
/// Applies DirectDiscount and DirectAndCoupon campaigns strictly within their [StartDate, EndDate) window.
/// </summary>
public sealed class CampaignPricingService(
    IDbContextFactory<AppDbContext> dbFactory,
    CollectionService collections,
    IMemoryCache cache,
    TimeProvider? timeProvider = null)
{
    private const string CacheKey = "campaign_pricing_live_rules_v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private sealed record CampaignProductOverrideRule(
        int ProductId,
        int? ProductVariantId,
        CampaignProductDiscountType DiscountType,
        decimal DiscountValue,
        decimal? MaxDiscountAmount);

    private sealed record CachedCampaignPricingRule(
        int CampaignId,
        string Name,
        string Slug,
        string BadgeLabel,
        string ThemeColor,
        DateTimeOffset? StartDate,
        DateTimeOffset? EndDate,
        int SortOrder,
        CampaignProductDiscountType? DefaultDiscountType,
        decimal? DefaultDiscountValue,
        decimal? MaxDiscountAmount,
        HashSet<int> CollectionProductIds,
        Dictionary<(int ProductId, int? VariantId), CampaignProductOverrideRule> ProductOverrides);

    public void InvalidateCache() => cache.Remove(CacheKey);

    public Task InvalidateCacheAsync()
    {
        cache.Remove(CacheKey);
        return Task.CompletedTask;
    }

    public static decimal ComputeDiscountedPrice(
        decimal sellingPrice,
        CampaignProductDiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount = null)
    {
        if (sellingPrice <= 0m || discountValue <= 0m)
        {
            return sellingPrice;
        }

        return discountType switch
        {
            CampaignProductDiscountType.Percentage => ComputePercentagePrice(sellingPrice, discountValue, maxDiscountAmount),
            CampaignProductDiscountType.FixedDiscount => ComputeFixedDiscountPrice(sellingPrice, discountValue, maxDiscountAmount),
            CampaignProductDiscountType.FixedPrice => Math.Max(0m, Math.Min(sellingPrice, discountValue)),
            _ => sellingPrice
        };
    }

    private static decimal ComputePercentagePrice(decimal sellingPrice, decimal percent, decimal? maxDiscount)
    {
        var clampedPercent = Math.Clamp(percent, 0m, 100m);
        var discount = Math.Round(sellingPrice * (clampedPercent / 100m), 0, MidpointRounding.AwayFromZero);
        if (maxDiscount is > 0m)
        {
            discount = Math.Min(discount, maxDiscount.Value);
        }
        return Math.Max(0m, sellingPrice - discount);
    }

    private static decimal ComputeFixedDiscountPrice(decimal sellingPrice, decimal fixedOff, decimal? maxDiscount)
    {
        var discount = fixedOff;
        if (maxDiscount is > 0m)
        {
            discount = Math.Min(discount, maxDiscount.Value);
        }
        return Math.Max(0m, sellingPrice - discount);
    }

    /// <summary>
    /// Resolves the best active campaign offer for each requested (ProductId, VariantId) pair.
    /// Precedence within a campaign: Variant-specific CampaignProduct > Product-wide CampaignProduct > Collection default discount.
    /// Across overlapping live campaigns: lowest EffectivePrice wins.
    /// </summary>
    public async Task<Dictionary<int, ActiveCampaignDiscountDto>> ResolveVariantOffersAsync(
        IReadOnlyList<(int ProductId, int VariantId, decimal SellingPrice, decimal? CompareAtPrice)> variants,
        CancellationToken ct = default)
    {
        var result = new Dictionary<int, ActiveCampaignDiscountDto>();
        if (variants.Count == 0)
        {
            return result;
        }

        var liveRules = await GetActiveWindowRulesAsync(ct);
        if (liveRules.Count == 0)
        {
            return result;
        }

        foreach (var (productId, variantId, sellingPrice, compareAtPrice) in variants)
        {
            if (sellingPrice <= 0m)
            {
                continue;
            }

            ActiveCampaignDiscountDto? bestOffer = null;

            foreach (var campaign in liveRules)
            {
                decimal? candidatePrice = null;

                // 1. Variant-specific CampaignProduct override
                if (variantId > 0 && campaign.ProductOverrides.TryGetValue((productId, variantId), out var variantRule))
                {
                    candidatePrice = ComputeDiscountedPrice(
                        sellingPrice,
                        variantRule.DiscountType,
                        variantRule.DiscountValue,
                        variantRule.MaxDiscountAmount);
                }
                // 2. Product-wide CampaignProduct override
                else if (campaign.ProductOverrides.TryGetValue((productId, null), out var productRule))
                {
                    candidatePrice = ComputeDiscountedPrice(
                        sellingPrice,
                        productRule.DiscountType,
                        productRule.DiscountValue,
                        productRule.MaxDiscountAmount);
                }
                // 3. Collection-wide default discount
                else if (campaign.DefaultDiscountType.HasValue
                    && campaign.DefaultDiscountValue is > 0m
                    && campaign.CollectionProductIds.Contains(productId))
                {
                    candidatePrice = ComputeDiscountedPrice(
                        sellingPrice,
                        campaign.DefaultDiscountType.Value,
                        campaign.DefaultDiscountValue.Value,
                        campaign.MaxDiscountAmount);
                }

                if (!candidatePrice.HasValue || candidatePrice.Value >= sellingPrice)
                {
                    continue;
                }

                var effectivePrice = candidatePrice.Value;
                if (bestOffer is not null && effectivePrice >= bestOffer.EffectivePrice)
                {
                    continue;
                }

                var regularPrice = compareAtPrice.HasValue && compareAtPrice.Value > sellingPrice
                    ? compareAtPrice.Value
                    : sellingPrice;
                var campaignSavings = sellingPrice - effectivePrice;
                var discountPercent = (int)Math.Round(campaignSavings / sellingPrice * 100m, 0, MidpointRounding.AwayFromZero);

                bestOffer = new ActiveCampaignDiscountDto(
                    CampaignId: campaign.CampaignId,
                    CampaignName: campaign.Name,
                    CampaignSlug: campaign.Slug,
                    BadgeLabel: campaign.BadgeLabel,
                    ThemeColor: campaign.ThemeColor,
                    EffectivePrice: effectivePrice,
                    RegularPrice: regularPrice,
                    SavingsAmount: regularPrice - effectivePrice,
                    DiscountPercent: Math.Clamp(discountPercent, 1, 99),
                    EndsAt: campaign.EndDate);
            }

            if (bestOffer is not null)
            {
                result[variantId] = bestOffer;
            }
        }

        return result;
    }

    /// <summary>
    /// Enriches product card summaries with active campaign offer prices and badges without mutating base inventory/collection queries.
    /// For products where a CampaignProduct override targets only non-default variants or differs across variants, resolves per default variant and falls back to lowest active product offer.
    /// </summary>
    public async Task<List<ProductSummaryDto>> ApplyCampaignPricingAsync(
        IReadOnlyList<ProductSummaryDto> products,
        CancellationToken ct = default)
    {
        if (products.Count == 0)
        {
            return [.. products];
        }

        var liveRules = await GetActiveWindowRulesAsync(ct);
        if (liveRules.Count == 0)
        {
            return [.. products];
        }

        var inputs = products
            .Select(p => (p.Id, VariantId: p.DefaultVariantId ?? 0, SellingPrice: p.Price, CompareAtPrice: p.OriginalPrice))
            .ToList();

        var defaultOffers = await ResolveVariantOffersByProductAndVariantAsync(inputs, liveRules);

        var updated = new List<ProductSummaryDto>(products.Count);
        foreach (var product in products)
        {
            if (defaultOffers.TryGetValue(product.Id, out var offer) && offer.EffectivePrice < product.Price)
            {
                var crossedOut = product.OriginalPrice.HasValue && product.OriginalPrice.Value > product.Price
                    ? product.OriginalPrice.Value
                    : product.Price;
                updated.Add(product with
                {
                    Price = offer.EffectivePrice,
                    OriginalPrice = crossedOut,
                    CampaignOffer = offer with
                    {
                        RegularPrice = crossedOut,
                        SavingsAmount = crossedOut - offer.EffectivePrice
                    }
                });
            }
            else
            {
                updated.Add(product);
            }
        }

        return updated;
    }

    private static Task<Dictionary<int, ActiveCampaignDiscountDto>> ResolveVariantOffersByProductAndVariantAsync(
        IReadOnlyList<(int ProductId, int VariantId, decimal SellingPrice, decimal? CompareAtPrice)> inputs,
        IReadOnlyList<CachedCampaignPricingRule> liveRules)
    {
        var result = new Dictionary<int, ActiveCampaignDiscountDto>();

        foreach (var (productId, variantId, sellingPrice, compareAtPrice) in inputs)
        {
            if (sellingPrice <= 0m)
            {
                continue;
            }

            ActiveCampaignDiscountDto? bestOffer = null;

            foreach (var campaign in liveRules)
            {
                decimal? candidatePrice = null;

                if (variantId > 0 && campaign.ProductOverrides.TryGetValue((productId, variantId), out var variantRule))
                {
                    candidatePrice = ComputeDiscountedPrice(
                        sellingPrice,
                        variantRule.DiscountType,
                        variantRule.DiscountValue,
                        variantRule.MaxDiscountAmount);
                }
                else if (campaign.ProductOverrides.TryGetValue((productId, null), out var productRule))
                {
                    candidatePrice = ComputeDiscountedPrice(
                        sellingPrice,
                        productRule.DiscountType,
                        productRule.DiscountValue,
                        productRule.MaxDiscountAmount);
                }
                else if (campaign.DefaultDiscountType.HasValue
                    && campaign.DefaultDiscountValue is > 0m
                    && campaign.CollectionProductIds.Contains(productId))
                {
                    candidatePrice = ComputeDiscountedPrice(
                        sellingPrice,
                        campaign.DefaultDiscountType.Value,
                        campaign.DefaultDiscountValue.Value,
                        campaign.MaxDiscountAmount);
                }

                if (!candidatePrice.HasValue || candidatePrice.Value >= sellingPrice)
                {
                    continue;
                }

                var effectivePrice = candidatePrice.Value;
                if (bestOffer is not null && effectivePrice >= bestOffer.EffectivePrice)
                {
                    continue;
                }

                var regularPrice = compareAtPrice.HasValue && compareAtPrice.Value > sellingPrice
                    ? compareAtPrice.Value
                    : sellingPrice;
                var discountPercent = (int)Math.Round((sellingPrice - effectivePrice) / sellingPrice * 100m, 0, MidpointRounding.AwayFromZero);

                bestOffer = new ActiveCampaignDiscountDto(
                    CampaignId: campaign.CampaignId,
                    CampaignName: campaign.Name,
                    CampaignSlug: campaign.Slug,
                    BadgeLabel: campaign.BadgeLabel,
                    ThemeColor: campaign.ThemeColor,
                    EffectivePrice: effectivePrice,
                    RegularPrice: regularPrice,
                    SavingsAmount: regularPrice - effectivePrice,
                    DiscountPercent: Math.Clamp(discountPercent, 1, 99),
                    EndsAt: campaign.EndDate);
            }

            if (bestOffer is not null)
            {
                result[productId] = bestOffer;
            }
        }

        return Task.FromResult(result);
    }

    private async Task<List<CachedCampaignPricingRule>> GetActiveWindowRulesAsync(CancellationToken ct)
    {
        var allRules = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var campaigns = await db.Campaigns.AsNoTracking()
                .Include(c => c.CampaignProducts)
                .Where(c => c.IsActive
                    && (c.DiscountScenario == CampaignDiscountScenario.DirectDiscount
                        || c.DiscountScenario == CampaignDiscountScenario.DirectAndCoupon))
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .ToListAsync(ct);

            var list = new List<CachedCampaignPricingRule>(campaigns.Count);
            foreach (var c in campaigns)
            {
                var collectionIds = new HashSet<int>();
                if (c.CollectionId.HasValue && c.DefaultDiscountType.HasValue && c.DefaultDiscountValue is > 0m)
                {
                    var ids = await collections.GetCollectionProductIdsAsync(c.CollectionId.Value, ct);
                    foreach (var id in ids)
                    {
                        collectionIds.Add(id);
                    }
                }

                var overrides = new Dictionary<(int ProductId, int? VariantId), CampaignProductOverrideRule>();
                foreach (var cp in c.CampaignProducts)
                {
                    overrides[(cp.ProductId, cp.ProductVariantId)] = new CampaignProductOverrideRule(
                        cp.ProductId,
                        cp.ProductVariantId,
                        cp.DiscountType,
                        cp.DiscountValue,
                        cp.MaxDiscountAmount);
                }

                var badgeLabel = !string.IsNullOrWhiteSpace(c.BadgeText) ? c.BadgeText.Trim() : c.Name;

                list.Add(new CachedCampaignPricingRule(
                    CampaignId: c.Id,
                    Name: c.Name,
                    Slug: c.Slug,
                    BadgeLabel: badgeLabel,
                    ThemeColor: string.IsNullOrWhiteSpace(c.ThemeColor) ? "rose" : c.ThemeColor,
                    StartDate: c.StartDate,
                    EndDate: c.EndDate,
                    SortOrder: c.SortOrder,
                    DefaultDiscountType: c.DefaultDiscountType,
                    DefaultDiscountValue: c.DefaultDiscountValue,
                    MaxDiscountAmount: c.MaxDiscountAmount,
                    CollectionProductIds: collectionIds,
                    ProductOverrides: overrides));
            }

            return list;
        }) ?? [];

        var now = _time.GetUtcNow();
        return allRules
            .Where(r => (!r.StartDate.HasValue || r.StartDate.Value <= now)
                && (!r.EndDate.HasValue || r.EndDate.Value > now))
            .ToList();
    }
}
