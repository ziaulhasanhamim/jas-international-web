using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Endpoints;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace JASIntWeb.Tests.Integration;

public class CartEnhancedFormIntegrationTests : JasIntegrationTestBase
{

    [Test]
    public async Task Cart_AddToCart_FormPost_SetsGuidCookieAndSavesToDb()
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
        await location!.Should().NotContain("cart=added");
        await location!.Should().NotStartWith("/cart");

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var cartIdCookie = setCookies.FirstOrDefault(c => c.Contains("jas_cart_id="));
        await cartIdCookie.Should().NotBeNull();

        // Extract GUID
        var match = Regex.Match(cartIdCookie!, @"jas_cart_id=([a-fA-F0-9\-]+)");
        await match.Success.Should().BeTrue();
        var parsed = Guid.TryParse(match.Groups[1].Value, out var cartToken);
        await parsed.Should().BeTrue();

        // Verify no cart payload in cookies
        var hasGuestCartJson = setCookies.Any(c => c.Contains("jas_guest_cart="));
        var hasCartCount = setCookies.Any(c => c.Contains("jas_cart_count="));
        await hasGuestCartJson.Should().BeFalse();
        await hasCartCount.Should().BeFalse();

        // Verify cart exists in PostgreSQL DB
        var cartInDb = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Token == cartToken);
        await cartInDb.Should().NotBeNull();
        await cartInDb!.Items.Any(i => i.ProductVariantId == variant.Id && i.Quantity >= 2).Should().BeTrue();
    }

    [Test]
    public async Task Cart_AddToCart_FormPost_NeverRedirectsToCartPage()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => !v.IsDeleted && v.IsActive);
        if (variant == null) return;

        // 1. With returnUrl pointing to product
        var form1 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "productId", variant.ProductId.ToString() },
            { "variantId", variant.Id.ToString() },
            { "quantity", "1" },
            { "returnUrl", $"/product/{variant.Product?.Slug ?? "test"}" }
        });
        var res1 = await client.PostAsync("/cart/add", form1);
        var loc1 = res1.Headers.Location?.ToString();
        await loc1.Should().NotBeNull();
        await loc1!.Should().StartWith("/product/");
        await loc1!.Should().NotStartWith("/cart");

        // 2. Without returnUrl (defaults to staying on page / referer / root, not /cart)
        var form2 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "productId", variant.ProductId.ToString() },
            { "variantId", variant.Id.ToString() },
            { "quantity", "1" }
        });
        var res2 = await client.PostAsync("/cart/add", form2);
        var loc2 = res2.Headers.Location?.ToString();
        await loc2.Should().NotBeNull();
        await loc2!.Should().NotStartWith("/cart");
    }

    [Test]
    public async Task Cart_BuyNow_FormPost_SetsGuidCookieAndRedirectsToCart()
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
        await location!.Should().BeEqualTo("/cart");

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var cartIdCookie = setCookies.FirstOrDefault(c => c.Contains("jas_cart_id="));
        await cartIdCookie.Should().NotBeNull();
    }

    [Test]
    public async Task SiteHeader_WithCartGuidCookie_RendersCountFromDbOnSSR()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => !v.IsDeleted && v.IsActive);
        if (variant == null)
        {
            return;
        }

        // Create a test cart in DB with token
        var testToken = Guid.CreateVersion7();
        var cart = new Cart
        {
            Token = testToken,
            Status = CartStatus.Active,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };
        cart.Items.Add(new CartItem
        {
            ProductVariantId = variant.Id,
            Quantity = 4
        });
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var client = Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", $"jas_cart_id={testToken}");

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await html.Should().Contain("data-cart-count");
        await html.Should().Contain("4");
    }

    [Test]
    public async Task CartCleanup_PurgesConvertedAndInactiveCartsOlderThan30Days()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => !v.IsDeleted && v.IsActive);
        if (variant == null)
        {
            return;
        }

        // 1. Converted cart (should be deleted)
        var convertedCart = new Cart
        {
            Token = Guid.CreateVersion7(),
            Status = CartStatus.Converted,
            LastActivityAt = DateTimeOffset.UtcNow
        };
        convertedCart.Items.Add(new CartItem { ProductVariantId = variant.Id, Quantity = 1 });

        // 2. Inactive >30d cart (should be deleted)
        var oldInactiveCart = new Cart
        {
            Token = Guid.CreateVersion7(),
            Status = CartStatus.Active,
            LastActivityAt = DateTimeOffset.UtcNow.AddDays(-35)
        };
        oldInactiveCart.Items.Add(new CartItem { ProductVariantId = variant.Id, Quantity = 2 });

        // 3. Active fresh cart (should NOT be deleted)
        var activeFreshCart = new Cart
        {
            Token = Guid.CreateVersion7(),
            Status = CartStatus.Active,
            LastActivityAt = DateTimeOffset.UtcNow
        };
        activeFreshCart.Items.Add(new CartItem { ProductVariantId = variant.Id, Quantity = 3 });

        db.Carts.AddRange(convertedCart, oldInactiveCart, activeFreshCart);
        await db.SaveChangesAsync();

        var cleanup = new CartCleanupBackgroundService(dbFactory, NullLogger<CartCleanupBackgroundService>.Instance);
        var deletedCount = await cleanup.CleanupStaleCartsAsync();

        await (deletedCount >= 2).Should().BeTrue();

        // Verify fresh active cart is still present
        var freshInDb = await db.Carts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == activeFreshCart.Id);
        await freshInDb.Should().NotBeNull();

        // Verify converted and old carts are gone
        var convertedInDb = await db.Carts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == convertedCart.Id);
        var oldInDb = await db.Carts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == oldInactiveCart.Id);
        await convertedInDb.Should().BeNull();
        await oldInDb.Should().BeNull();
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
        await html.Should().Contain("data-add-to-cart");
        await html.Should().Contain("name=\"Quantity\"");
        await html.Should().Contain("Buy Now");
        await html.Should().Contain("data-wishlist-toggle");
    }

    [Test]
    public async Task AddToCart_ThenNavigateToCartPage_ShowsItemsImmediatelyOnSSR()
    {
        var noRedirectClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 1. Post to /cart/add
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "productId", "1" },
            { "variantId", "1" },
            { "quantity", "2" },
            { "returnUrl", "/product/anker-737-power-bank" }
        });
        var addRes = await noRedirectClient.PostAsync("/cart/add", form);
        await ((int)addRes.StatusCode).Should().BeEqualTo((int)HttpStatusCode.Redirect);

        var cookies = addRes.Headers.GetValues("Set-Cookie").ToList();
        var cartCookie = cookies.FirstOrDefault(c => c.Contains("jas_cart_id="));
        await cartCookie.Should().NotBeNull();
        var match = Regex.Match(cartCookie!, @"jas_cart_id=([a-fA-F0-9\-]+)");
        await match.Success.Should().BeTrue();
        var token = match.Groups[1].Value;

        // 2. Direct Enhanced Navigation GET to /cart (with the cookie)
        var client = Factory.CreateClient();
        var cartRequest = new HttpRequestMessage(HttpMethod.Get, "/cart");
        cartRequest.Headers.Add("Cookie", $"jas_cart_id={token}");
        var cartResponse = await client.SendAsync(cartRequest);

        await ((int)cartResponse.StatusCode).Should().BeEqualTo(200);
        var html = await cartResponse.Content.ReadAsStringAsync();

        // 3. Verify items show up IMMEDIATELY without reload or client hydration
        await html.Should().Contain("Anker 737 Power Bank");
        await html.Should().Contain("Your Shopping Cart");
        await html.Should().Contain("2 items");
        await html.Should().Contain("Decrease quantity");
        await html.Should().Contain("Increase quantity");
        await html.Should().NotContain("Your cart is empty");
        await html.Should().NotContain("Loading your cart…");
    }

    [Test]
    public async Task Cart_UpdateQuantity_FormPost_UpdatesLineItemAndReflectsOnCartPage()
    {
        var noRedirectClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 1. Seed item in cart
        var addForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "productId", "1" },
            { "variantId", "1" },
            { "quantity", "1" }
        });
        var addRes = await noRedirectClient.PostAsync("/cart/add", addForm);
        var cookieHeader = addRes.Headers.GetValues("Set-Cookie").First(c => c.Contains("jas_cart_id="));
        var token = Regex.Match(cookieHeader, @"jas_cart_id=([a-fA-F0-9\-]+)").Groups[1].Value;

        // 2. Post update to quantity 5
        var updateForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "variantId", "1" },
            { "quantity", "5" }
        });
        var updateReq = new HttpRequestMessage(HttpMethod.Post, "/cart/update") { Content = updateForm };
        updateReq.Headers.Add("Cookie", $"jas_cart_id={token}");
        var updateRes = await noRedirectClient.SendAsync(updateReq);

        await ((int)updateRes.StatusCode).Should().BeEqualTo((int)HttpStatusCode.Redirect);
        await updateRes.Headers.Location!.ToString().Should().BeEqualTo("/cart");

        // 3. GET /cart and verify 5 items
        var client = Factory.CreateClient();
        var cartReq = new HttpRequestMessage(HttpMethod.Get, "/cart");
        cartReq.Headers.Add("Cookie", $"jas_cart_id={token}");
        var cartRes = await client.SendAsync(cartReq);

        var html = await cartRes.Content.ReadAsStringAsync();
        await html.Should().Contain("5 items");
        await html.Should().Contain("Anker 737 Power Bank");

        // 4. Remove item (quantity = 0)
        var removeForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "variantId", "1" },
            { "quantity", "0" }
        });
        var removeReq = new HttpRequestMessage(HttpMethod.Post, "/cart/update") { Content = removeForm };
        removeReq.Headers.Add("Cookie", $"jas_cart_id={token}");
        var removeRes = await noRedirectClient.SendAsync(removeReq);
        await ((int)removeRes.StatusCode).Should().BeEqualTo((int)HttpStatusCode.Redirect);

        // 5. GET /cart and verify empty
        var emptyReq = new HttpRequestMessage(HttpMethod.Get, "/cart");
        emptyReq.Headers.Add("Cookie", $"jas_cart_id={token}");
        var emptyRes = await client.SendAsync(emptyReq);

        var emptyHtml = await emptyRes.Content.ReadAsStringAsync();
        await emptyHtml.Should().Contain("Your cart is empty");
    }

    [Test]
    public async Task AddToCart_WhenQuantityExceedsAvailableStock_ClampsToAvailableStockAndNeverReturns500()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();

        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => !v.IsDeleted && v.IsActive);
        if (variant == null) return;

        var resolved = await shop.ResolveAsync([new(variant.Id, 1)]);
        var stock = resolved.Items[0].Stock;
        if (stock <= 0) return;

        var token = Guid.NewGuid();
        // Add quantity equal to stock
        var initial = await shop.ChangeAsync(token, null, new CartLineRequest(variant.Id, stock), add: true);
        await initial.Items.First(i => i.VariantId == variant.Id).Quantity.Should().BeEqualTo(stock);

        // Try adding more - should throw ShopException with friendly message, clamped
        var ex = await Assert.ThrowsAsync<ShopException>(async () =>
        {
            await shop.ChangeAsync(token, null, new CartLineRequest(variant.Id, 1), add: true);
        });
        await (ex?.Message ?? "").Should().Contain("Maximum available stock");

        // Now test via HTTP /cart/add endpoint with rapid clicks (never 500)
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var req = new HttpRequestMessage(HttpMethod.Post, "/cart/add")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "productId", variant.ProductId.ToString() },
                { "variantId", variant.Id.ToString() },
                { "quantity", "99" }
            })
        };
        req.Headers.Add("Cookie", $"jas_cart_id={token}");
        var res = await client.SendAsync(req);

        // Must redirect cleanly without crashing or 500 error
        await ((int)res.StatusCode).Should().BeEqualTo((int)HttpStatusCode.Redirect);
    }

    [Test]
    public async Task ProductDetails_ReviewSubmission_SavesReviewDirectlyToDatabase()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.FirstOrDefaultAsync(p => !p.IsDeleted);
        if (product == null) return;

        var client = Factory.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "_handler", "submit-review" },
            { "X-CSRF-TOKEN", "test-request-token" },
            { "Name", "Hasan Tester" },
            { "Email", "hasan@jasinternational.test" },
            { "Rating", "5" },
            { "Title", "Top quality gadget" },
            { "Comment", "Battery lasts for 2 days as advertised. Fully authentic product with warranty." }
        });

        var response = await client.PostAsync($"/product/{product.Slug}", form);
        await ((int)response.StatusCode).Should().BeEqualTo(200);

        var reviewInDb = await db.ProductReviews.FirstOrDefaultAsync(r => r.ProductId == product.Id && r.CustomerName == "Hasan Tester");
        await reviewInDb.Should().NotBeNull();
        await reviewInDb!.Rating.Should().BeEqualTo(5);
        await reviewInDb.Comment.Should().Contain("Battery lasts");
    }

    [Test]
    public async Task AdminCategories_BulkActions_AppliesHideRestoreAndDelete()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cat1 = new Category { Name = "Bulk Cat 1", Slug = "bulk-cat-1-" + Guid.NewGuid().ToString("N")[..6] };
        var cat2 = new Category { Name = "Bulk Cat 2", Slug = "bulk-cat-2-" + Guid.NewGuid().ToString("N")[..6] };
        db.Categories.AddRange(cat1, cat2);
        await db.SaveChangesAsync();

        var ids = new List<int> { cat1.Id, cat2.Id };

        // 1. Bulk Hide
        await admin.BulkCategoryActionAsync(ids, "hide");
        var hiddenCheck = await db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        await hiddenCheck.All(c => c.IsDeleted).Should().BeTrue();

        // 2. Bulk Restore
        await admin.BulkCategoryActionAsync(ids, "restore");
        var restoreCheck = await db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        await restoreCheck.All(c => !c.IsDeleted).Should().BeTrue();

        // 3. Bulk Delete (0 products -> hard delete)
        await admin.BulkCategoryActionAsync(ids, "delete");
        var deletedCheck = await db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        await deletedCheck.Should().BeEmpty();
    }

    [Test]
    public async Task AdminProducts_BulkActions_AppliesArchivePublishAndCategoryMove()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var catA = new Category { Name = "Category Alpha", Slug = "cat-alpha-" + Guid.NewGuid().ToString("N")[..6] };
        var catB = new Category { Name = "Category Beta", Slug = "cat-beta-" + Guid.NewGuid().ToString("N")[..6] };
        db.Categories.AddRange(catA, catB);
        await db.SaveChangesAsync();

        var prod = new Product
        {
            Name = "Bulk Product Test",
            Slug = "bulk-prod-" + Guid.NewGuid().ToString("N")[..6],
            CategoryId = catA.Id,
            Details = "Test Details",
            MainImageUrl = "/images/test.webp"
        };
        db.Products.Add(prod);
        await db.SaveChangesAsync();

        var ids = new List<int> { prod.Id };

        // 1. Bulk Archive
        await admin.BulkProductActionAsync(ids, "archive");
        var archived = await db.Products.AsNoTracking().FirstAsync(p => p.Id == prod.Id);
        await archived.IsDeleted.Should().BeTrue();

        // 2. Bulk Publish
        await admin.BulkProductActionAsync(ids, "publish");
        var published = await db.Products.AsNoTracking().FirstAsync(p => p.Id == prod.Id);
        await published.IsDeleted.Should().BeFalse();

        // 3. Bulk Move Category
        await admin.BulkProductActionAsync(ids, "category", catB.Id);
        var moved = await db.Products.AsNoTracking().FirstAsync(p => p.Id == prod.Id);
        await moved.CategoryId.Should().BeEqualTo(catB.Id);
    }
}
