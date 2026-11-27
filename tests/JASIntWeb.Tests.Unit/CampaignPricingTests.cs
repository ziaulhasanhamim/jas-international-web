using JASIntWeb.Client.Dtos;
using JASIntWeb.Services;

namespace JASIntWeb.Tests.Unit;

public class CampaignPricingTests
{
    [Test]
    public async Task ComputeDiscountedPrice_Percentage_CalculatesAndRoundsAwayFromZero()
    {
        var price1 = CampaignPricingService.ComputeDiscountedPrice(2000m, CampaignProductDiscountType.Percentage, 11m);
        var price2 = CampaignPricingService.ComputeDiscountedPrice(1555m, CampaignProductDiscountType.Percentage, 15m);

        await price1.Should().BeEqualTo(1780m);
        await price2.Should().BeEqualTo(1322m); // 1555 * 0.15 = 233.25 -> 233 -> 1322
    }

    [Test]
    public async Task ComputeDiscountedPrice_Percentage_RespectsMaxDiscountCap()
    {
        var capped = CampaignPricingService.ComputeDiscountedPrice(10000m, CampaignProductDiscountType.Percentage, 50m, maxDiscountAmount: 2000m);

        await capped.Should().BeEqualTo(8000m);
    }

    [Test]
    public async Task ComputeDiscountedPrice_FixedDiscount_AppliesFloorAndCap()
    {
        var normal = CampaignPricingService.ComputeDiscountedPrice(1800m, CampaignProductDiscountType.FixedDiscount, 300m);
        var capped = CampaignPricingService.ComputeDiscountedPrice(5000m, CampaignProductDiscountType.FixedDiscount, 1500m, maxDiscountAmount: 1000m);
        var floored = CampaignPricingService.ComputeDiscountedPrice(400m, CampaignProductDiscountType.FixedDiscount, 600m);

        await normal.Should().BeEqualTo(1500m);
        await capped.Should().BeEqualTo(4000m);
        await floored.Should().BeEqualTo(0m);
    }

    [Test]
    public async Task ComputeDiscountedPrice_FixedPrice_NeverExceedsRegularSellingPrice()
    {
        var flashPrice = CampaignPricingService.ComputeDiscountedPrice(2500m, CampaignProductDiscountType.FixedPrice, 1111m);
        var higherThanRegular = CampaignPricingService.ComputeDiscountedPrice(900m, CampaignProductDiscountType.FixedPrice, 1200m);

        await flashPrice.Should().BeEqualTo(1111m);
        await higherThanRegular.Should().BeEqualTo(900m);
    }
}
