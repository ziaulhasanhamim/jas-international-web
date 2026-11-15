using System.Net;
using JASIntWeb.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

[NotInParallel("web-integration")]
public class CartEnhancedFormIntegrationTests
{
    private static readonly WebApplicationFactory<Program> Factory = new();

    [Test]
    public async Task Cart_AddToCart_FormPost_SetsCookieAndRedirects()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => !v.IsDeleted && v.IsActive);
        if (variant == null)
        {
            return;
        }

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "productId", variant.ProductId.ToString() },
            { "variantId", variant.Id.ToString() },
            { "quantity", "2" },
            { "returnUrl", $"/product/{variant.Product?.Slug ?? "test"}" }
        });

        var response = await client.PostAsync("/cart/add", formContent);
        await ((int)response.StatusCode).Should().BeEqualTo((int)HttpStatusCode.Redirect);

        var location = response.Headers.Location?.ToString();
        await location.Should().NotBeNull();
        await location!.Should().Contain("cart=added");

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var hasCountCookie = setCookies.Any(c => c.Contains("jas_cart_count="));
        var hasGuestCart = setCookies.Any(c => c.Contains("jas_guest_cart="));

        await hasCountCookie.Should().BeTrue();
        await hasGuestCart.Should().BeTrue();
    }

    [Test]
    public async Task Cart_BuyNow_FormPost_SetsCookieAndRedirectsToCart()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => !v.IsDeleted && v.IsActive);
        if (variant == null)
        {
            return;
        }

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "productId", variant.ProductId.ToString() },
            { "variantId", variant.Id.ToString() },
            { "quantity", "1" }
        });

        var response = await client.PostAsync("/cart/buy-now", formContent);
        await ((int)response.StatusCode).Should().BeEqualTo((int)HttpStatusCode.Redirect);

        var location = response.Headers.Location?.ToString();
        await location.Should().NotBeNull();
        await location!.Should().StartWith("/cart");

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var hasCountCookie = setCookies.Any(c => c.Contains("jas_cart_count="));
        await hasCountCookie.Should().BeTrue();
    }

    [Test]
    public async Task SiteHeader_WithCartCountCookie_RendersCountOnSSR()
    {
        var client = Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", "jas_cart_count=3");

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await html.Should().Contain("data-cart-count");
        await html.Should().Contain("3");
    }

    [Test]
    public async Task ProductDetails_RendersEnhancedFormWithQuantityStepperAndBuyNow()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.Include(p => p.ProductVariants)
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.ProductVariants.Any(v => !v.IsDeleted && v.IsActive));

        if (product == null)
        {
            return;
        }

        var client = Factory.CreateClient();
        var response = await client.GetAsync($"/product/{product.Slug}");
        var html = await response.Content.ReadAsStringAsync();

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await html.Should().Contain("action=\"/cart/add\"");
        await html.Should().Contain("formaction=\"/cart/buy-now\"");
        await html.Should().Contain("name=\"quantity\"");
        await html.Should().Contain("data-wishlist-toggle");
    }
}
