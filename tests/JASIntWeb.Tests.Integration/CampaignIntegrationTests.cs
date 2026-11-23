using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class CampaignIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task CampaignService_AndStorefrontHub_ReturnsActiveCampaigns()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var campaignSvc = scope.ServiceProvider.GetRequiredService<CampaignService>();

        // 1. Create a Tag
        var tag = new Tag
        {
            Name = "Winter Fest Deals",
            Slug = "winter-fest-deals",
            BadgeColor = "emerald",
            IsHidden = false
        };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();

        // 2. Create Category and Product
        var cat = new Category { Name = "Campaign Category", Slug = "campaign-cat" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        var prod = new Product { Name = "Winter Special Camera", Slug = "winter-special-camera", Details = "", CategoryId = cat.Id, MainImageUrl = "" };
        var variant = new ProductVariant { SKU = "WTR-CAM-01", SellingPrice = 5000m, IsDefault = true, Product = prod };
        prod.ProductVariants.Add(variant);
        db.Products.Add(prod);
        await db.SaveChangesAsync();

        // Tag the product
        db.ProductTags.Add(new ProductTag { ProductId = prod.Id, TagId = tag.Id });

        // 3. Create Campaign
        var campaign = new Campaign
        {
            Name = "Winter Mega Sale 2026",
            Slug = "winter-mega-sale-2026",
            BanglaName = "শীতকালীন মেগা সেল",
            Eyebrow = "FESTIVAL SALE · UP TO 40% OFF",
            Description = "Exclusive discounts on winter gadgets and creator equipment.",
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(7),
            ShowCountdown = true,
            ThemeColor = "emerald",
            HighlightCouponCode = "WINTER26",
            TagId = tag.Id,
            IsActive = true
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        await campaignSvc.InvalidateCacheAsync(campaign.Slug);

        // 4. Test Service queries
        var active = await campaignSvc.GetActiveCampaignsAsync();
        await active.Any(c => c.Slug == "winter-mega-sale-2026").Should().BeTrue();

        var details = await campaignSvc.GetCampaignBySlugAsync("winter-mega-sale-2026");
        await details.Should().NotBeNull();
        await details!.Name.Should().BeEqualTo("Winter Mega Sale 2026");
        await details.HighlightCouponCode.Should().BeEqualTo("WINTER26");
        await details.Products.Count.Should().BeGreaterThanOrEqualTo(1);
        await details.Products.Any(p => p.Id == prod.Id).Should().BeTrue();

        // 5. Test HTTP GET /offers
        var hubHtml = await AnonymousClient.GetStringAsync("/offers");
        await hubHtml.Contains("Winter Mega Sale 2026").Should().BeTrue();
        await hubHtml.Contains("WINTER26").Should().BeTrue();

        // 6. Test HTTP GET /campaigns/{slug}
        var pageHtml = await AnonymousClient.GetStringAsync($"/campaigns/{campaign.Slug}");
        await pageHtml.Contains("Winter Mega Sale 2026").Should().BeTrue();
        await pageHtml.Contains("Winter Special Camera").Should().BeTrue();
    }
}
