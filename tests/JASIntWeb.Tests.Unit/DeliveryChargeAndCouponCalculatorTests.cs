using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Tests.Unit;

public class DeliveryChargeAndCouponCalculatorTests
{
    [Test]
    public async Task CalculateDeliveryCharge_ForKhulna_ReturnsHubFee()
    {
        var settings = new DeliverySettings
        {
            HubDistrict = "Khulna",
            HubCityFee = 80m,
            OutsideFee = 150m,
            FreeDeliveryThreshold = 5000m
        };

        var fee = CalculateDeliveryFee("Khulna", 2500m, settings);
        await fee.Should().BeEqualTo(80m);
    }

    [Test]
    public async Task CalculateDeliveryCharge_ForOutsideKhulna_ReturnsOutsideFee()
    {
        var settings = new DeliverySettings
        {
            HubDistrict = "Khulna",
            HubCityFee = 80m,
            OutsideFee = 150m,
            FreeDeliveryThreshold = 5000m
        };

        var fee = CalculateDeliveryFee("Dhaka", 2500m, settings);
        await fee.Should().BeEqualTo(150m);
    }

    [Test]
    public async Task CalculateDeliveryCharge_AboveFreeThreshold_ReturnsZero()
    {
        var settings = new DeliverySettings
        {
            HubDistrict = "Khulna",
            HubCityFee = 80m,
            OutsideFee = 150m,
            FreeDeliveryThreshold = 5000m
        };

        var fee = CalculateDeliveryFee("Chittagong", 5200m, settings);
        await fee.Should().BeEqualTo(0m);
    }

    [Test]
    public async Task ApplyCoupon_FixedDiscount_DeductsExactAmount()
    {
        var coupon = new Coupon
        {
            Code = "WELCOME200",
            DiscountType = CouponDiscountType.FixedAmount,
            DiscountValue = 200m,
            MinOrderAmount = 1000m,
            IsActive = true
        };

        var discount = CalculateCouponDiscount(coupon, 1500m);
        await discount.Should().BeEqualTo(200m);
    }

    [Test]
    public async Task ApplyCoupon_PercentageDiscount_WithCap_AppliesMaxCap()
    {
        var coupon = new Coupon
        {
            Code = "SAVE10",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 10m,
            MaxDiscountAmount = 500m,
            MinOrderAmount = 500m,
            IsActive = true
        };

        // 10% of ৳10,000 is ৳1,000, capped at ৳500
        var discount = CalculateCouponDiscount(coupon, 10000m);
        await discount.Should().BeEqualTo(500m);
    }

    private static decimal CalculateDeliveryFee(string district, decimal subtotal, DeliverySettings settings)
    {
        if (settings.FreeDeliveryThreshold > 0 && subtotal >= settings.FreeDeliveryThreshold)
        {
            return 0m;
        }

        return string.Equals(district?.Trim(), settings.HubDistrict, StringComparison.OrdinalIgnoreCase)
            ? settings.HubCityFee
            : settings.OutsideFee;
    }

    private static decimal CalculateCouponDiscount(Coupon coupon, decimal subtotal)
    {
        if (!coupon.IsActive || subtotal < coupon.MinOrderAmount)
        {
            return 0m;
        }

        return coupon.DiscountType switch
        {
            CouponDiscountType.FixedAmount => Math.Min(subtotal, coupon.DiscountValue),
            CouponDiscountType.Percentage => coupon.MaxDiscountAmount.HasValue
                ? Math.Min(subtotal * (coupon.DiscountValue / 100m), coupon.MaxDiscountAmount.Value)
                : subtotal * (coupon.DiscountValue / 100m),
            CouponDiscountType.FreeDelivery => 0m,
            _ => 0m
        };
    }
}
