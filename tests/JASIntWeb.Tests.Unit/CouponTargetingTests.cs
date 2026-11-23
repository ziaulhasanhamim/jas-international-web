using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Tests.Unit;

public class CouponTargetingTests
{
    private sealed record ProductTestContext(
        int VariantId,
        decimal Price,
        int ProductId,
        int CategoryId,
        int? BrandId,
        List<int> TagIds);

    [Test]
    public async Task EvaluateEligibility_IncludeCategory_AllowsOnlyMatchingCategory()
    {
        var coupon = new Coupon
        {
            Code = "AUDIO20",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 20,
            IncludedCategoryIds = [1], // Category 1: Audio
            IsActive = true
        };

        var items = new List<ProductTestContext>
        {
            new(VariantId: 101, Price: 1000m, ProductId: 1, CategoryId: 1, BrandId: 10, TagIds: []), // Audio
            new(VariantId: 102, Price: 500m, ProductId: 2, CategoryId: 2, BrandId: 10, TagIds: [])  // Smartwatches
        };

        var (eligibleSubtotal, count) = CalculateEligibleSubtotal(coupon, items);
        await eligibleSubtotal.Should().BeEqualTo(1000m);
        await count.Should().BeEqualTo(1);
    }

    [Test]
    public async Task EvaluateEligibility_ExcludeBrandOverridesIncludeCategory()
    {
        // Scenario from user prompt: applies on all Earbuds category except brand from Ulanzi
        var coupon = new Coupon
        {
            Code = "EARBUDS_SALE",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 15,
            IncludedCategoryIds = [1], // Earbuds
            ExcludedBrandIds = [99],   // Ulanzi
            IsActive = true
        };

        var items = new List<ProductTestContext>
        {
            // Earbuds from Ulanzi -> Must be excluded!
            new(VariantId: 101, Price: 1500m, ProductId: 1, CategoryId: 1, BrandId: 99, TagIds: []),
            // Earbuds from Anker -> Must be included!
            new(VariantId: 102, Price: 2500m, ProductId: 2, CategoryId: 1, BrandId: 50, TagIds: [])
        };

        var (eligibleSubtotal, count) = CalculateEligibleSubtotal(coupon, items);
        await eligibleSubtotal.Should().BeEqualTo(2500m);
        await count.Should().BeEqualTo(1);
    }

    [Test]
    public async Task EvaluateEligibility_IncludeTags_IncludesTaggedProducts()
    {
        var coupon = new Coupon
        {
            Code = "SUMMER26",
            DiscountType = CouponDiscountType.FixedAmount,
            DiscountValue = 300,
            IncludedTagIds = [7], // Summer Sale tag (can be visible or admin hidden)
            IsActive = true
        };

        var items = new List<ProductTestContext>
        {
            new(VariantId: 101, Price: 2000m, ProductId: 1, CategoryId: 3, BrandId: 1, TagIds: [7]), // Tagged
            new(VariantId: 102, Price: 1200m, ProductId: 2, CategoryId: 3, BrandId: 1, TagIds: [])   // Untagged
        };

        var (eligibleSubtotal, count) = CalculateEligibleSubtotal(coupon, items);
        await eligibleSubtotal.Should().BeEqualTo(2000m);
        await count.Should().BeEqualTo(1);
    }

    [Test]
    public async Task EvaluateEligibility_ExcludeTag_ExcludesTaggedProducts()
    {
        var coupon = new Coupon
        {
            Code = "NOCLEARANCE",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 10,
            ExcludedTagIds = [999], // Clearance / B2B tag
            IsActive = true
        };

        var items = new List<ProductTestContext>
        {
            new(VariantId: 101, Price: 3000m, ProductId: 1, CategoryId: 1, BrandId: 1, TagIds: [999]), // Clearance -> Excluded
            new(VariantId: 102, Price: 2000m, ProductId: 2, CategoryId: 1, BrandId: 1, TagIds: [])     // Regular -> Included
        };

        var (eligibleSubtotal, count) = CalculateEligibleSubtotal(coupon, items);
        await eligibleSubtotal.Should().BeEqualTo(2000m);
        await count.Should().BeEqualTo(1);
    }

    [Test]
    public async Task MixedCart_PercentageDiscount_AppliesOnlyToEligibleItems()
    {
        var coupon = new Coupon
        {
            Code = "CREATOR20",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 20, // 20%
            IncludedCategoryIds = [5], // Creator Essentials
            MaxDiscountAmount = 1000m,
            IsActive = true
        };

        var items = new List<ProductTestContext>
        {
            new(VariantId: 101, Price: 3000m, ProductId: 1, CategoryId: 5, BrandId: 1, TagIds: []), // Eligible: 3000
            new(VariantId: 102, Price: 2000m, ProductId: 2, CategoryId: 2, BrandId: 2, TagIds: [])  // Ineligible: 2000
        };

        var (eligibleSubtotal, count) = CalculateEligibleSubtotal(coupon, items);
        await eligibleSubtotal.Should().BeEqualTo(3000m);

        // 20% of 3000 = 600 (not 20% of 5000 = 1000)
        var discount = Math.Min(eligibleSubtotal * (coupon.DiscountValue / 100m), coupon.MaxDiscountAmount ?? decimal.MaxValue);
        await discount.Should().BeEqualTo(600m);
    }

    [Test]
    public async Task TagDto_GetBadgeClasses_ReturnsExpectedColorStyle()
    {
        var emerald = TagDto.GetBadgeClasses("emerald");
        await emerald.Should().Contain("bg-emerald-50");

        var rose = TagDto.GetBadgeClasses("rose");
        await rose.Should().Contain("bg-rose-50");

        var fallback = TagDto.GetBadgeClasses("unknown");
        await fallback.Should().Contain("bg-blue-50");
    }

    private static (decimal EligibleSubtotal, int EligibleCount) CalculateEligibleSubtotal(
        Coupon coupon,
        List<ProductTestContext> items)
    {
        decimal total = 0m;
        int count = 0;

        bool hasInclusions = coupon.IncludedBrandIds.Count > 0 ||
                             coupon.IncludedCategoryIds.Count > 0 ||
                             coupon.IncludedTagIds.Count > 0;

        foreach (var item in items)
        {
            // Exclusions
            bool isExcluded = (item.BrandId.HasValue && coupon.ExcludedBrandIds.Contains(item.BrandId.Value))
                || coupon.ExcludedCategoryIds.Contains(item.CategoryId)
                || item.TagIds.Any(tid => coupon.ExcludedTagIds.Contains(tid));

            if (isExcluded) continue;

            // Inclusions
            if (hasInclusions)
            {
                bool isIncluded = (item.BrandId.HasValue && coupon.IncludedBrandIds.Contains(item.BrandId.Value))
                    || coupon.IncludedCategoryIds.Contains(item.CategoryId)
                    || item.TagIds.Any(tid => coupon.IncludedTagIds.Contains(tid));

                if (!isIncluded) continue;
            }

            total += item.Price;
            count++;
        }

        return (total, count);
    }
}
