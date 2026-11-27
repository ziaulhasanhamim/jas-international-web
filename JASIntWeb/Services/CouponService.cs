using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Services;

public sealed record AppliedCoupon(
    string Code,
    decimal DiscountAmount,
    CouponDiscountType DiscountType,
    string Message
);

public record CouponValidationResult(
    bool IsValid,
    string? Code,
    decimal DiscountAmount,
    string? Message,
    CouponDiscountType? DiscountType = null
);

public sealed class CouponService(
    AppDbContext db,
    CollectionService collectionService,
    CampaignPricingService? campaignPricing = null)
{
    public async Task<Result<AppliedCoupon>> ApplyCouponAsync(
        string code,
        decimal subtotal,
        decimal deliveryCharge = 0,
        IReadOnlyList<CartLineRequest>? items = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.Validation("Please enter a coupon code.");
        }

        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized && c.IsActive);

        if (coupon == null)
        {
            return Error.NotFound("Invalid coupon code.");
        }

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTimeOffset.UtcNow)
        {
            return Error.Validation("This coupon has expired.");
        }

        if (coupon.UsageLimit.HasValue && coupon.TimesUsed >= coupon.UsageLimit.Value)
        {
            return Error.Validation("This coupon's usage limit has been reached.");
        }

        decimal eligibleSubtotal = subtotal;
        string? collectionLabel = null;

        if (coupon.HasTargetingRules)
        {
            if (items == null || items.Count == 0)
            {
                return Error.Validation("This coupon requires cart items to verify eligibility.");
            }

            var variantIds = items.Select(i => i.VariantId).Distinct().ToList();
            var variantData = await db.ProductVariants.AsNoTracking()
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => new
                {
                    v.Id,
                    v.SellingPrice,
                    v.CompareAtPrice,
                    v.ProductId,
                    CategoryId = v.Product!.CategoryId,
                    BrandId = v.Product.BrandId,
                    TagIds = v.Product.ProductTags.Select(pt => pt.TagId).ToList()
                })
                .ToListAsync();

            var variantOffers = campaignPricing is not null && variantData.Count > 0
                ? await campaignPricing.ResolveVariantOffersAsync(
                    variantData.Select(v => (v.ProductId, v.Id, v.SellingPrice, v.CompareAtPrice)).ToList())
                : new Dictionary<int, ActiveCampaignDiscountDto>();

            decimal EffectiveUnitPrice(int variantId, decimal regularSellingPrice) =>
                variantOffers.TryGetValue(variantId, out var offer) && offer.EffectivePrice < regularSellingPrice
                    ? offer.EffectivePrice
                    : regularSellingPrice;

            var variantMap = variantData.ToDictionary(v => v.Id);
            decimal targetEligibleSubtotal = 0m;
            bool hasEligibleItem = false;

            if (coupon.CollectionId.HasValue)
            {
                var meta = await collectionService.GetCollectionMetaAsync(coupon.CollectionId.Value);
                if (meta == null || !meta.IsActive)
                {
                    return Error.Validation("This coupon's promotional collection is currently inactive.");
                }

                collectionLabel = meta.Name;
                var allowedProductIds = (await collectionService.GetCollectionProductIdsAsync(coupon.CollectionId.Value)).ToHashSet();

                foreach (var item in items)
                {
                    if (!variantMap.TryGetValue(item.VariantId, out var v)) continue;
                    if (!allowedProductIds.Contains(v.ProductId)) continue;

                    hasEligibleItem = true;
                    targetEligibleSubtotal += EffectiveUnitPrice(v.Id, v.SellingPrice) * item.Quantity;
                }

                if (!hasEligibleItem || targetEligibleSubtotal <= 0)
                {
                    return Error.Validation($"This coupon only applies to products in the '{collectionLabel}' collection.");
                }
            }
            else
            {
                bool hasInclusions = coupon.IncludedBrandIds.Count > 0 ||
                                     coupon.IncludedCategoryIds.Count > 0 ||
                                     coupon.IncludedTagIds.Count > 0;

                foreach (var item in items)
                {
                    if (!variantMap.TryGetValue(item.VariantId, out var v)) continue;

                    bool isExcluded = (v.BrandId.HasValue && coupon.ExcludedBrandIds.Contains(v.BrandId.Value))
                        || coupon.ExcludedCategoryIds.Contains(v.CategoryId)
                        || v.TagIds.Any(tid => coupon.ExcludedTagIds.Contains(tid));

                    if (isExcluded) continue;

                    if (hasInclusions)
                    {
                        bool isIncluded = (v.BrandId.HasValue && coupon.IncludedBrandIds.Contains(v.BrandId.Value))
                            || coupon.IncludedCategoryIds.Contains(v.CategoryId)
                            || v.TagIds.Any(tid => coupon.IncludedTagIds.Contains(tid));

                        if (!isIncluded) continue;
                    }

                    hasEligibleItem = true;
                    targetEligibleSubtotal += EffectiveUnitPrice(v.Id, v.SellingPrice) * item.Quantity;
                }

                if (!hasEligibleItem || targetEligibleSubtotal <= 0)
                {
                    return Error.Validation("This coupon is not valid for any items in your cart.");
                }
            }

            eligibleSubtotal = targetEligibleSubtotal;
        }

        if (coupon.MinOrderAmount.HasValue && eligibleSubtotal < coupon.MinOrderAmount.Value)
        {
            return Error.Validation(coupon.HasTargetingRules
                ? $"Eligible items subtotal must be at least ৳{coupon.MinOrderAmount.Value:N0} to use this coupon."
                : $"Order subtotal must be at least ৳{coupon.MinOrderAmount.Value:N0} to use this coupon.");
        }

        decimal discount = coupon.DiscountType switch
        {
            CouponDiscountType.Percentage => coupon.MaxDiscountAmount.HasValue
                ? Math.Min(Math.Round(eligibleSubtotal * (coupon.DiscountValue / 100m), 2), coupon.MaxDiscountAmount.Value)
                : Math.Round(eligibleSubtotal * (coupon.DiscountValue / 100m), 2),
            CouponDiscountType.FixedAmount => Math.Min(coupon.DiscountValue, eligibleSubtotal),
            CouponDiscountType.FreeDelivery => deliveryCharge,
            _ => 0m
        };

        string successMessage = !string.IsNullOrWhiteSpace(collectionLabel)
            ? $"Coupon '{coupon.Code}' applied to '{collectionLabel}' items!"
            : (coupon.HasTargetingRules
                ? $"Coupon '{coupon.Code}' applied to eligible products!"
                : $"Coupon '{coupon.Code}' applied successfully!");

        return new AppliedCoupon(coupon.Code, discount, coupon.DiscountType, successMessage);
    }

    public async Task<CouponValidationResult> ValidateCouponAsync(
        string code,
        decimal subtotal,
        decimal deliveryCharge = 0,
        IReadOnlyList<CartLineRequest>? items = null)
    {
        var result = await ApplyCouponAsync(code, subtotal, deliveryCharge, items);
        return result switch
        {
            AppliedCoupon c => new CouponValidationResult(true, c.Code, c.DiscountAmount, c.Message, c.DiscountType),
            Error err => new CouponValidationResult(false, code.Trim().ToUpperInvariant(), 0, err.Message)
        };
    }

    public async Task RecordCouponUseAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == normalized);
        if (coupon != null)
        {
            coupon.TimesUsed++;
            await db.SaveChangesAsync();
        }
    }
}
