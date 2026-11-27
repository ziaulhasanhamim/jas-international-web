using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class CampaignPricingIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task ScheduledCampaignPricing_EnforcesStartEndWindows_Precedence_AndAllFourScenarios()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pricingSvc = scope.ServiceProvider.GetRequiredService<CampaignPricingService>();
        var catalogSvc = scope.ServiceProvider.GetRequiredService<CatalogService>();
        var collectionSvc = scope.ServiceProvider.GetRequiredService<CollectionService>();
        var campaignSvc = scope.ServiceProvider.GetRequiredService<CampaignService>();
        var couponSvc = scope.ServiceProvider.GetRequiredService<CouponService>();

        var cat = new Category { Name = $"Promo Cat {suffix}", Slug = $"promo-cat-{suffix}" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        var pDefault = new Product { Name = $"Collection Watch {suffix}", Slug = $"col-watch-{suffix}", Details = "", CategoryId = cat.Id, MainImageUrl = "/jas-logo.png" };
        var vDefault = new ProductVariant { SKU = $"CW-{suffix}", SellingPrice = 2000m, IsDefault = true, IsActive = true, Product = pDefault };
        pDefault.ProductVariants.Add(vDefault);

        var pOverride = new Product { Name = $"Flash Bud {suffix}", Slug = $"flash-bud-{suffix}", Details = "", CategoryId = cat.Id, MainImageUrl = "/jas-logo.png" };
        var vOverrideBase = new ProductVariant { SKU = $"FB1-{suffix}", OptionSignature = "standard", SellingPrice = 2500m, IsDefault = true, IsActive = true, Product = pOverride };
        var vOverrideSpecific = new ProductVariant { SKU = $"FB2-{suffix}", OptionSignature = "pro", SellingPrice = 3000m, IsDefault = false, IsActive = true, Product = pOverride };
        pOverride.ProductVariants.Add(vOverrideBase);
        pOverride.ProductVariants.Add(vOverrideSpecific);

        var pCouponOnly = new Product { Name = $"Coupon Mic {suffix}", Slug = $"coupon-mic-{suffix}", Details = "", CategoryId = cat.Id, MainImageUrl = "/jas-logo.png" };
        var vCouponOnly = new ProductVariant { SKU = $"CM-{suffix}", SellingPrice = 4000m, IsDefault = true, IsActive = true, Product = pCouponOnly };
        pCouponOnly.ProductVariants.Add(vCouponOnly);

        var pMarketing = new Product { Name = $"Showcase Phone {suffix}", Slug = $"showcase-phone-{suffix}", Details = "", CategoryId = cat.Id, MainImageUrl = "/jas-logo.png" };
        var vMarketing = new ProductVariant { SKU = $"SP-{suffix}", SellingPrice = 5000m, IsDefault = true, IsActive = true, Product = pMarketing };
        pMarketing.ProductVariants.Add(vMarketing);

        var pFuture = new Product { Name = $"Upcoming Dock {suffix}", Slug = $"upcoming-dock-{suffix}", Details = "", CategoryId = cat.Id, MainImageUrl = "/jas-logo.png" };
        var vFuture = new ProductVariant { SKU = $"UD-{suffix}", SellingPrice = 1500m, IsDefault = true, IsActive = true, Product = pFuture };
        pFuture.ProductVariants.Add(vFuture);

        db.Products.AddRange(pDefault, pOverride, pCouponOnly, pMarketing, pFuture);
        await db.SaveChangesAsync();

        var saleCollection = new Collection
        {
            Name = $"11.11 Deals {suffix}",
            Slug = $"deals-col-{suffix}",
            Type = CollectionType.Manual,
            IsActive = true
        };
        var stackCouponCollection = new Collection
        {
            Name = $"Stackable Col {suffix}",
            Slug = $"stack-col-{suffix}",
            Type = CollectionType.Manual,
            IsActive = true
        };
        db.Collections.AddRange(saleCollection, stackCouponCollection);
        await db.SaveChangesAsync();

        db.CollectionProducts.AddRange(
            new CollectionProduct { CollectionId = saleCollection.Id, ProductId = pDefault.Id, SortOrder = 1 },
            new CollectionProduct { CollectionId = saleCollection.Id, ProductId = pOverride.Id, SortOrder = 2 },
            new CollectionProduct { CollectionId = stackCouponCollection.Id, ProductId = pDefault.Id, SortOrder = 1 },
            new CollectionProduct { CollectionId = stackCouponCollection.Id, ProductId = pCouponOnly.Id, SortOrder = 2 }
        );

        var stackCoupon = new Coupon
        {
            Code = $"STACK10_{suffix}".ToUpperInvariant(),
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 10m,
            CollectionId = stackCouponCollection.Id,
            IsActive = true
        };
        db.Coupons.Add(stackCoupon);
        await db.SaveChangesAsync();

        // Scenario 1: DirectAndCoupon (live 11.11 campaign: 11% collection default + per-product FixedPrice + variant-specific FixedDiscount)
        var liveCampaign = new Campaign
        {
            Name = $"11.11 Sale {suffix}",
            Slug = $"11-11-{suffix}",
            BadgeText = "11.11 DEAL",
            StartDate = DateTimeOffset.UtcNow.AddHours(-2),
            EndDate = DateTimeOffset.UtcNow.AddDays(3),
            ShowCountdown = true,
            DiscountScenario = CampaignDiscountScenario.DirectAndCoupon,
            DefaultDiscountType = CampaignProductDiscountType.Percentage,
            DefaultDiscountValue = 11m,
            CollectionId = saleCollection.Id,
            CouponId = stackCoupon.Id,
            HighlightCouponCode = stackCoupon.Code,
            IsActive = true,
            SortOrder = 1
        };

        // Scenario 2: CouponOnly (must NOT alter catalog price)
        var couponOnlyCampaign = new Campaign
        {
            Name = $"Coupon Only {suffix}",
            Slug = $"coupon-only-{suffix}",
            StartDate = DateTimeOffset.UtcNow.AddHours(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(3),
            DiscountScenario = CampaignDiscountScenario.CouponOnly,
            CouponId = stackCoupon.Id,
            HighlightCouponCode = stackCoupon.Code,
            CuratedProductIds = [pCouponOnly.Id],
            IsActive = true,
            SortOrder = 2
        };

        // Scenario 3: MarketingOnly (showcase only, no automatic price cut)
        var marketingCampaign = new Campaign
        {
            Name = $"Launch Showcase {suffix}",
            Slug = $"launch-{suffix}",
            StartDate = DateTimeOffset.UtcNow.AddHours(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            DiscountScenario = CampaignDiscountScenario.MarketingOnly,
            CuratedProductIds = [pMarketing.Id],
            IsActive = true,
            SortOrder = 3
        };

        // Scenario 4: Future campaign (StartDate in future -> upcoming teaser only, NOT discounted yet)
        var futureCampaign = new Campaign
        {
            Name = $"Upcoming Sale {suffix}",
            Slug = $"upcoming-{suffix}",
            StartDate = DateTimeOffset.UtcNow.AddHours(6),
            EndDate = DateTimeOffset.UtcNow.AddDays(7),
            DiscountScenario = CampaignDiscountScenario.DirectDiscount,
            DefaultDiscountType = CampaignProductDiscountType.Percentage,
            DefaultDiscountValue = 50m,
            IsActive = true,
            SortOrder = 4
        };

        // Expired campaign (must NOT apply)
        var expiredCampaign = new Campaign
        {
            Name = $"Expired Sale {suffix}",
            Slug = $"expired-{suffix}",
            StartDate = DateTimeOffset.UtcNow.AddDays(-5),
            EndDate = DateTimeOffset.UtcNow.AddHours(-1),
            DiscountScenario = CampaignDiscountScenario.DirectDiscount,
            DefaultDiscountType = CampaignProductDiscountType.Percentage,
            DefaultDiscountValue = 80m,
            CollectionId = saleCollection.Id,
            IsActive = true,
            SortOrder = 0
        };

        db.Campaigns.AddRange(liveCampaign, couponOnlyCampaign, marketingCampaign, futureCampaign, expiredCampaign);
        await db.SaveChangesAsync();

        db.CampaignProducts.AddRange(
            new CampaignProduct
            {
                CampaignId = liveCampaign.Id,
                ProductId = pOverride.Id,
                ProductVariantId = null, // Product-wide override
                DiscountType = CampaignProductDiscountType.FixedPrice,
                DiscountValue = 1111m,
                SortOrder = 1
            },
            new CampaignProduct
            {
                CampaignId = liveCampaign.Id,
                ProductId = pOverride.Id,
                ProductVariantId = vOverrideSpecific.Id, // Variant-specific override takes highest precedence
                DiscountType = CampaignProductDiscountType.FixedDiscount,
                DiscountValue = 500m,
                SortOrder = 2
            },
            new CampaignProduct
            {
                CampaignId = futureCampaign.Id,
                ProductId = pFuture.Id,
                DiscountType = CampaignProductDiscountType.FixedPrice,
                DiscountValue = 750m,
                SortOrder = 1
            },
            new CampaignProduct
            {
                CampaignId = marketingCampaign.Id,
                ProductId = pMarketing.Id,
                DiscountType = CampaignProductDiscountType.Percentage,
                DiscountValue = 25m, // MarketingOnly must ignore this discount!
                SortOrder = 1
            }
        );
        await db.SaveChangesAsync();

        await pricingSvc.InvalidateCacheAsync();
        await collectionSvc.InvalidateCacheAsync(saleCollection.Id);
        await campaignSvc.InvalidateCacheAsync();
        await catalogSvc.InvalidateAllAsync();

        // Verify Product Details resolution
        var defaultDetails = await catalogSvc.DetailsAsync(pDefault.Slug);
        await defaultDetails.Should().NotBeNull();
        await defaultDetails!.Variants[0].Price.Should().BeEqualTo(1780m); // 2000 - 11% = 1780
        await defaultDetails.Variants[0].OriginalPrice.Should().BeEqualTo(2000m);
        await defaultDetails.Variants[0].CampaignOffer!.BadgeLabel.Should().BeEqualTo("11.11 DEAL");

        // Verify per-product override vs variant-specific override
        var overrideDetails = await catalogSvc.DetailsAsync(pOverride.Slug);
        await overrideDetails.Should().NotBeNull();
        var baseVarDto = overrideDetails!.Variants.Single(v => v.Id == vOverrideBase.Id);
        var specVarDto = overrideDetails.Variants.Single(v => v.Id == vOverrideSpecific.Id);
        await baseVarDto.Price.Should().BeEqualTo(1111m);
        await specVarDto.Price.Should().BeEqualTo(2500m); // 3000 - 500 = 2500

        // Verify CouponOnly & MarketingOnly & Upcoming do NOT discount catalog prices
        var couponOnlyDetails = await catalogSvc.DetailsAsync(pCouponOnly.Slug);
        await couponOnlyDetails!.Variants[0].Price.Should().BeEqualTo(4000m);
        await couponOnlyDetails.Variants[0].CampaignOffer.Should().BeNull();

        var marketingDetails = await catalogSvc.DetailsAsync(pMarketing.Slug);
        await marketingDetails!.Variants[0].Price.Should().BeEqualTo(5000m);
        await marketingDetails.Variants[0].CampaignOffer.Should().BeNull();

        var futureDetails = await catalogSvc.DetailsAsync(pFuture.Slug);
        await futureDetails!.Variants[0].Price.Should().BeEqualTo(1500m);
        await futureDetails.Variants[0].CampaignOffer.Should().BeNull();

        // Verify Coupon stacking computes 10% off the LIVE campaign discounted price (1780 -> 178 discount, not 200)
        var couponRes = await couponSvc.ApplyCouponAsync(
            stackCoupon.Code,
            subtotal: 1780m,
            deliveryCharge: 100m,
            items: [new CartLineRequest(vDefault.Id, 1)]);

        var applied = couponRes switch
        {
            AppliedCoupon c => c,
            _ => null
        };
        await applied.Should().NotBeNull();
        await applied!.DiscountAmount.Should().BeEqualTo(178m);

        // Verify ShoppingService.ResolveAsync returns discounted campaign CartItemDto
        var shoppingSvc = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        var resolvedRes = await shoppingSvc.ResolveAsync([new CartLineRequest(vDefault.Id, 2)], authenticated: false);
        var resolvedCart = resolvedRes switch
        {
            CartDto dto => dto,
            _ => null
        };
        await resolvedCart.Should().NotBeNull();
        await resolvedCart!.Items.Count.Should().BeEqualTo(1);
        await resolvedCart.Items[0].Price.Should().BeEqualTo(1780m);
        await resolvedCart.Items[0].RegularPrice.Should().BeEqualTo(2000m);
        await resolvedCart.Items[0].CampaignSlug.Should().BeEqualTo(liveCampaign.Slug);
        await resolvedCart.Subtotal.Should().BeEqualTo(3560m);

        // Verify /offers/{slug} route alias & /campaigns/{slug} render active campaign deals
        var offerHtml = await AnonymousClient.GetStringAsync($"/offers/{liveCampaign.Slug}");
        await offerHtml.Contains(liveCampaign.Name).Should().BeTrue();
        await offerHtml.Contains("Tk. 1,111").Should().BeTrue();

        // Verify upcoming campaign shows regular price + upcoming offer price preview on /offers/{futureCampaign.Slug}
        var futureDetailsCheck = await campaignSvc.GetCampaignBySlugAsync(futureCampaign.Slug);
        await futureDetailsCheck.Should().NotBeNull();
        var futureProdSummary = futureDetailsCheck!.Products.Single(p => p.Id == pFuture.Id);
        await futureProdSummary.Price.Should().BeEqualTo(1500m);
        await futureProdSummary.CampaignOffer.Should().NotBeNull();
        await futureProdSummary.CampaignOffer!.IsUpcoming.Should().BeTrue();
        await futureProdSummary.CampaignOffer.EffectivePrice.Should().BeEqualTo(750m);

        var upcomingHtml = await AnonymousClient.GetStringAsync($"/offers/{futureCampaign.Slug}");
        await upcomingHtml.Contains("Tk. 1,500").Should().BeTrue();
        await upcomingHtml.Contains("Tk. 750").Should().BeTrue();
    }
}
