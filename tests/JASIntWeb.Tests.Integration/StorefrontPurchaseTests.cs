using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace JASIntWeb.Tests.Integration;

public class StorefrontPurchaseTests : JasIntegrationTestBase
{
    [Test]
    public async Task ConfiguredProductCard_BuysDefaultVariantAndRedirectsToCart()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attribute = new AttributeDefinition { Name = "Color", Code = "color" };
        db.AttributeDefinitions.Add(attribute); await db.SaveChangesAsync();
        db.ProductAttributeDefinitions.Add(new() { ProductId = 1, AttributeDefinitionId = attribute.Id, IsRequired = false });
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogService>().InvalidateAllAsync();
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var html = await client.GetStringAsync("/products");
        var card = Regex.Matches(html, "<article\\b[\\s\\S]*?</article>").Single(match => match.Value.Contains("data-wishlist-toggle=\"1\"")).Value;
        await card.Contains("data-buy-now").Should().BeTrue();
        await card.Contains("data-variant-id=\"1\"").Should().BeTrue();
        await card.Contains("Choose options").Should().BeFalse();
        var response = await client.PostAsync("/cart/buy-now", new FormUrlEncodedContent(new Dictionary<string, string> { ["productId"] = "1", ["variantId"] = "1", ["quantity"] = "1" }));
        await response.StatusCode.Should().BeEqualTo(HttpStatusCode.Redirect);
        await response.Headers.Location!.ToString().Should().BeEqualTo("/cart");
        var cart = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;
        await cart.Items.Single().VariantId.Should().BeEqualTo(1);
        await cart.Items.Single().Quantity.Should().BeEqualTo(1);
    }

    [Test]
    public async Task RelatedProducts_ExcludeCurrentBeforeLimitAndRenderBesideSpecifications()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var current = await db.Products.Include(p => p.Category).SingleAsync(p => p.Id == 1);
        var other = await db.Products.SingleAsync(p => p.Id == 2);
        other.CategoryId = current.CategoryId;
        for (var i = 0; i < 5; i++)
        {
            db.Products.Add(new() { Name = "Related " + i, Slug = "related-" + i, CategoryId = current.CategoryId, Details = "Related fixture", MainImageUrl = "/jas-logo.png",
                ProductVariants = [new() { SKU = "RELATED-" + i, SellingPrice = 100 + i, IsActive = true, IsDefault = true }] });
        }
        db.Products.Add(new() { Name = "Hidden related", Slug = "hidden-related", CategoryId = current.CategoryId, Details = "Hidden", MainImageUrl = "/jas-logo.png", IsDeleted = true,
            ProductVariants = [new() { SKU = "HIDDEN-RELATED", SellingPrice = 100, IsActive = true }] });
        await db.SaveChangesAsync();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();
        await catalog.InvalidateAllAsync();
        var related = await catalog.LatestAsync(current.Category!.Slug, 4, current.Id);
        await related.Count.Should().BeEqualTo(4);
        await related.Any(p => p.Id == current.Id || p.Slug == "hidden-related").Should().BeFalse();
        await related.All(p => p.Category == current.Category!.Name).Should().BeTrue();
        using var client = AnonymousClient;
        var html = await client.GetStringAsync("/product/" + current.Slug);
        var details = html.IndexOf("Detailed Specifications", StringComparison.Ordinal);
        var recommendations = html.IndexOf("id=\"related-products-title\"", StringComparison.Ordinal);
        var reviews = html.IndexOf("Customer Ratings", StringComparison.Ordinal);
        await (details >= 0 && recommendations > details && reviews > recommendations).Should().BeTrue();
        await Regex.Matches(html, "id=\"related-products-title\"").Count.Should().BeEqualTo(1);
    }
}
