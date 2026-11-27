using System.Net;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class CollectionsIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task ManualCollection_RespectsExplicitSortOrder_AndDoesNotExposePublicPageWithoutCollectionPage()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var colSvc = scope.ServiceProvider.GetRequiredService<CollectionService>();

        var cat = new Category { Name = "Col Cat Manual", Slug = "col-cat-manual" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        var p1 = CreateTestProduct("First Created Gear", "col-p1", cat.Id, 1000m, "COL-P1");
        var p2 = CreateTestProduct("Second Created Gear", "col-p2", cat.Id, 2000m, "COL-P2");
        var p3 = CreateTestProduct("Third Created Gear", "col-p3", cat.Id, 3000m, "COL-P3");
        db.Products.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();
        await AddStockAsync(db, (p1, 5), (p2, 5), (p3, 5));

        var manualCol = new Collection
        {
            Name = "Manual Top Picks",
            Slug = "manual-only-internal",
            Type = CollectionType.Manual,
            SortMode = CollectionSortOrder.ManualOrder,
            IsActive = true
        };
        db.Collections.Add(manualCol);
        await db.SaveChangesAsync();

        // Explicitly order: p3 (#1), p1 (#2), p2 (#3)
        db.CollectionProducts.AddRange(
            new CollectionProduct { CollectionId = manualCol.Id, ProductId = p3.Id, SortOrder = 0 },
            new CollectionProduct { CollectionId = manualCol.Id, ProductId = p1.Id, SortOrder = 1 },
            new CollectionProduct { CollectionId = manualCol.Id, ProductId = p2.Id, SortOrder = 2 }
        );
        await db.SaveChangesAsync();
        await colSvc.InvalidateCacheAsync(manualCol.Id);

        var ordered = await colSvc.GetCollectionProductsAsync(manualCol.Id);
        await ordered.Select(x => x.Id).ToArray().Should().BeEquivalentTo([p3.Id, p1.Id, p2.Id]);

        // Without a CollectionPage, /collections/{slug} must return 404!
        var resp = await AnonymousClient.GetAsync($"/collections/{manualCol.Slug}");
        await ((int)resp.StatusCode).Should().BeEqualTo(404);
    }

    [Test]
    public async Task RuleBasedCollection_UpdatesDynamically_WhenPriceOrStockChanges()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var colSvc = scope.ServiceProvider.GetRequiredService<CollectionService>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();

        var micCat = new Category { Name = "Microphones Test", Slug = "microphones-test" };
        db.Categories.Add(micCat);
        var eidTag = new Tag { Name = "Eid Test Tag", Slug = "eid-test-tag" };
        db.Tags.Add(eidTag);
        await db.SaveChangesAsync();

        var micProd = CreateTestProduct("Budget Dynamic Mic", "budget-dyn-mic", micCat.Id, 2500m, "BD-MIC-1");
        db.Products.Add(micProd);
        await db.SaveChangesAsync();
        await AddStockAsync(db, (micProd, 4));

        var rules = new CollectionRuleGroup
        {
            MatchOperator = CollectionRuleLogicalOp.And,
            Groups =
            [
                new CollectionRuleGroup
                {
                    MatchOperator = CollectionRuleLogicalOp.Or,
                    Rules =
                    [
                        new CollectionRuleCondition { Field = CollectionRuleField.Tag, Operator = CollectionRuleOperator.Equals, Value = eidTag.Id.ToString() },
                        new CollectionRuleCondition { Field = CollectionRuleField.Category, Operator = CollectionRuleOperator.Equals, Value = micCat.Id.ToString() }
                    ]
                }
            ],
            Rules =
            [
                new CollectionRuleCondition { Field = CollectionRuleField.Stock, Operator = CollectionRuleOperator.GreaterThan, Value = "0" },
                new CollectionRuleCondition { Field = CollectionRuleField.Price, Operator = CollectionRuleOperator.LessThan, Value = "3000" }
            ]
        };

        var ruleCol = new Collection
        {
            Name = "Auto Eid & Mic Under 3000",
            Slug = "auto-eid-mic-under-3000",
            Type = CollectionType.RuleBased,
            SortMode = CollectionSortOrder.PriceLowToHigh,
            RulesJson = CollectionRuleEvaluator.SerializeRules(rules),
            IsActive = true
        };
        db.Collections.Add(ruleCol);
        await db.SaveChangesAsync();
        await colSvc.InvalidateCacheAsync(ruleCol.Id);

        // Initially matches (price 2500, stock 4)
        var initialIds = await colSvc.GetCollectionProductIdsAsync(ruleCol.Id);
        await initialIds.Contains(micProd.Id).Should().BeTrue();

        // Update price above 3000 -> must drop out automatically after cache invalidation
        var variant = await db.ProductVariants.FirstAsync(v => v.ProductId == micProd.Id);
        variant.SellingPrice = 3400m;
        await db.SaveChangesAsync();
        await catalog.InvalidateProductCacheAsync(micProd.Slug);

        var afterPriceIncrease = await colSvc.GetCollectionProductIdsAsync(ruleCol.Id);
        await afterPriceIncrease.Contains(micProd.Id).Should().BeFalse();

        // Drop price back to 1900, set stock to 0 -> still excluded
        variant.SellingPrice = 1900m;
        var inv = await db.InventoryItems.FirstAsync(i => i.ProductVariantId == variant.Id);
        inv.QuantityOnHand = 0;
        await db.SaveChangesAsync();
        await catalog.InvalidateProductCacheAsync(micProd.Slug);

        var afterOutOfStock = await colSvc.GetCollectionProductIdsAsync(ruleCol.Id);
        await afterOutOfStock.Contains(micProd.Id).Should().BeFalse();
    }

    [Test]
    public async Task CouponAndCampaign_WithSingleCollectionSelector_WorkEndToEnd()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var colSvc = scope.ServiceProvider.GetRequiredService<CollectionService>();
        var couponSvc = scope.ServiceProvider.GetRequiredService<CouponService>();
        var campaignSvc = scope.ServiceProvider.GetRequiredService<CampaignService>();

        var cat = new Category { Name = "Promo Collection Cat", Slug = "promo-col-cat" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        var eligible = CreateTestProduct("Eligible Gimbal", "eligible-gimbal", cat.Id, 4000m, "COL-ELIG-1");
        var excluded = CreateTestProduct("Non Collection Cable", "non-col-cable", cat.Id, 1000m, "COL-EXCL-1");
        db.Products.AddRange(eligible, excluded);
        await db.SaveChangesAsync();
        await AddStockAsync(db, (eligible, 5), (excluded, 5));

        var collection = new Collection
        {
            Name = "Creator Spotlight",
            Slug = "creator-spotlight-test",
            Type = CollectionType.Manual,
            SortMode = CollectionSortOrder.ManualOrder,
            IsActive = true
        };
        db.Collections.Add(collection);
        await db.SaveChangesAsync();

        db.CollectionProducts.Add(new CollectionProduct { CollectionId = collection.Id, ProductId = eligible.Id, SortOrder = 0 });

        var coupon = new Coupon
        {
            Code = "COLSPOT10",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 10,
            CollectionId = collection.Id,
            IsActive = true
        };
        db.Coupons.Add(coupon);

        var campaign = new Campaign
        {
            Name = "Creator Spotlight Fest",
            Slug = "creator-spotlight-fest",
            CollectionId = collection.Id,
            HighlightCouponCode = "COLSPOT10",
            IsActive = true
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        await colSvc.InvalidateCacheAsync(collection.Id);
        await campaignSvc.InvalidateCacheAsync(campaign.Slug);

        // Coupon on mixed cart applies 10% ONLY on eligible (4000 -> 400 discount, NOT 500)
        var eligVar = eligible.ProductVariants.First();
        var exclVar = excluded.ProductVariants.First();

        var mixedCart = new List<CartLineRequest> { new(eligVar.Id, 1), new(exclVar.Id, 1) };
        var couponResult = await couponSvc.ValidateCouponAsync("COLSPOT10", 5000m, 100m, mixedCart);
        await couponResult.IsValid.Should().BeTrue();
        await couponResult.DiscountAmount.Should().BeEqualTo(400m);

        // Coupon on cart with only ineligible item fails cleanly with collection message
        var invalidResult = await couponSvc.ValidateCouponAsync("COLSPOT10", 1000m, 100m, [new CartLineRequest(exclVar.Id, 1)]);
        await invalidResult.IsValid.Should().BeFalse();
        await invalidResult.Message!.Should().Contain("Creator Spotlight");

        // Campaign landing page shows collection product and ignores excluded item
        var campaignHtml = await AnonymousClient.GetStringAsync($"/campaigns/{campaign.Slug}");
        await campaignHtml.Should().Contain("Eligible Gimbal");
        await campaignHtml.Should().NotContain("Non Collection Cable");
    }

    [Test]
    public async Task PublishedCollectionPage_ServesHtmlWithProductsAndAppearsInSitemap_AndReturns404WhenUnpublished()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var colSvc = scope.ServiceProvider.GetRequiredService<CollectionService>();

        var cat = new Category { Name = "Audio Showcase Cat", Slug = "audio-showcase-cat" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        var p = CreateTestProduct("Showcase Wireless Headphones", "showcase-wireless-hp", cat.Id, 3200m, "SHW-HP-1");
        db.Products.Add(p);
        await db.SaveChangesAsync();
        await AddStockAsync(db, (p, 6));

        var col = new Collection
        {
            Name = "Showcase Collection Group",
            Slug = "showcase-collection-group",
            Type = CollectionType.Manual,
            IsActive = true
        };
        db.Collections.Add(col);
        await db.SaveChangesAsync();

        db.CollectionProducts.Add(new CollectionProduct { CollectionId = col.Id, ProductId = p.Id, SortOrder = 0 });

        var page = new CollectionPage
        {
            CollectionId = col.Id,
            Title = "Best Headphones Curated",
            Slug = "best-headphones-curated",
            Eyebrow = "HANDPICKED AUDIO",
            Description = "Top curated wireless headphones for music lovers.",
            BannerImageUrl = "/jas-og-card.webp",
            ThemeColor = "navy",
            IsPublished = true,
            ShowFilters = true
        };
        db.CollectionPages.Add(page);
        await db.SaveChangesAsync();
        await colSvc.InvalidateCacheAsync(col.Id);

        var resp = await AnonymousClient.GetAsync($"/collections/{page.Slug}");
        await ((int)resp.StatusCode).Should().BeEqualTo(200);
        var html = await resp.Content.ReadAsStringAsync();
        await html.Should().Contain("Best Headphones Curated");
        await html.Should().Contain("Top curated wireless headphones");
        await html.Should().Contain("Showcase Wireless Headphones");

        // Published collection page appears in sitemap-collections.xml (unlike internal collections)
        var sitemapResp = await AnonymousClient.GetAsync("/sitemap-collections.xml");
        await ((int)sitemapResp.StatusCode).Should().BeEqualTo(200);
        var sitemapXml = await sitemapResp.Content.ReadAsStringAsync();
        await sitemapXml.Should().Contain($"/collections/{page.Slug}");

        // Unpublish page -> returns 404
        page.IsPublished = false;
        await db.SaveChangesAsync();
        await colSvc.InvalidateCacheAsync(col.Id);

        var draftResp = await AnonymousClient.GetAsync($"/collections/{page.Slug}");
        await ((int)draftResp.StatusCode).Should().BeEqualTo(404);
    }

    private static Product CreateTestProduct(string name, string slug, int categoryId, decimal price, string sku)
    {
        var product = new Product
        {
            Name = name,
            Slug = slug,
            Details = "Test details",
            CategoryId = categoryId,
            MainImageUrl = "/jas-logo.png"
        };
        var variant = new ProductVariant
        {
            SKU = sku,
            SellingPrice = price,
            IsDefault = true,
            IsActive = true,
            Product = product
        };
        product.ProductVariants.Add(variant);
        return product;
    }

    private static async Task AddStockAsync(AppDbContext db, params (Product Product, int Stock)[] items)
    {
        foreach (var (prod, stock) in items)
        {
            var variant = prod.ProductVariants.First();
            db.InventoryItems.Add(new InventoryItem
            {
                ProductVariantId = variant.Id,
                QuantityOnHand = stock,
                QuantityReserved = 0
            });
        }
        await db.SaveChangesAsync();
    }
}
