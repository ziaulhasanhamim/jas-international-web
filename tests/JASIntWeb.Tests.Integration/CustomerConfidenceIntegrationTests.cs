using System.Net;
using System.Net.Http.Json;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JASIntWeb.Tests.Integration;

public class CustomerConfidenceIntegrationTests : JasIntegrationTestBase
{
    private static CheckoutRequest Request(string? coupon = null) => new()
    {
        RequestId = Guid.NewGuid(),
        CustomerName = "Test Customer",
        PhoneNumber = "01712345678",
        District = "Khulna",
        City = "Khulna",
        Address = "Test address, Khulna",
        PaymentMethod = "COD",
        Items = [new(1, 1)],
        CouponCode = coupon
    };

    private async Task<Order> CreateOrderAsync(int? userId = null)
    {
        await using var scope = CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ShoppingService>().CheckoutAsync(userId, Request());
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AsNoTracking().SingleAsync(o => o.Id == result.Unwrap().OrderId);
    }

    [Test]
    public async Task OrderAccess_AnonymousNumericReferencesAreDeniedAcrossAllSurfaces()
    {
        var order = await CreateOrderAsync();
        using var client = AnonymousClient;
        foreach (var path in new[] { $"/order/{order.Id}", $"/api/orders/{order.Id}/status", $"/api/orders/{order.Id}/sse", $"/api/orders/{order.Id}/payment-status", $"/payment-verification?orderid={order.Id}", $"/api/orders/payment-sse?orderid={order.Id}", $"/api/orders/payment-sse?token={order.Id}", $"/payment-verification?token={order.Id}", $"/ORDER/{order.Id}" })
        {
            using var response = await client.GetAsync(path);
            await ((int)response.StatusCode).Should().BeEqualTo(404);
            await response.Headers.CacheControl!.NoStore.Should().BeTrue();
            await (await response.Content.ReadAsStringAsync()).Contains(order.PublicToken.ToString()).Should().BeFalse();
        }
    }

    [Test]
    public async Task OrderAccess_PrivateTokenOwnerAndAdminWorkButAnotherCustomerDoesNot()
    {
        var order = await CreateOrderAsync(2);
        using var anonymous = AnonymousClient;
        using var owner = CustomerClient;
        using var admin = AdminClient;
        using var other = CustomerClient;
        other.DefaultRequestHeaders.Remove("X-Test-UserId"); other.DefaultRequestHeaders.Add("X-Test-UserId", "999");
        await ((int)(await anonymous.GetAsync($"/api/orders/{order.PublicToken}/status")).StatusCode).Should().BeEqualTo(200);
        await ((int)(await owner.GetAsync($"/api/orders/{order.Id}/status")).StatusCode).Should().BeEqualTo(200);
        await ((int)(await admin.GetAsync($"/api/orders/{order.Id}/status")).StatusCode).Should().BeEqualTo(200);
        await ((int)(await other.GetAsync($"/api/orders/{order.Id}/status")).StatusCode).Should().BeEqualTo(404);
        await ((int)(await anonymous.GetAsync($"/order/{order.PublicToken}")).StatusCode).Should().BeEqualTo(200);
    }

    [Test]
    public async Task PaymentVerification_ReplayDoesNotDuplicateTimelineAndPreservesFulfillment()
    {
        var order = await CreateOrderAsync();
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Orders.Where(o => o.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.InTransit));
        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        await shop.VerifyPaymentAsync(order.Id, 150m, "REPLAY123");
        await shop.VerifyPaymentAsync(order.Id, 150m, "REPLAY123");
        var persisted = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        await persisted.Status.Should().BeEqualTo(OrderStatus.InTransit);
        await persisted.PaidAmount.Should().BeEqualTo(150m);
        await (await db.OrderTimelineEvents.CountAsync(e => e.OrderId == order.Id && e.Title == "Payment Verified")).Should().BeEqualTo(1);
        await (await db.Payments.CountAsync(p => p.OrderId == order.Id && p.Status == PaymentStatus.Completed)).Should().BeEqualTo(1);
    }

    [Test]
    public async Task PaymentVerification_RejectsReusedTransactionInvalidAmountAndCancelledOrder()
    {
        var first = await CreateOrderAsync(); var second = await CreateOrderAsync();
        await using var scope = CreateScope();
        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        await shop.VerifyPaymentAsync(first.Id, 150m, "UNIQUE123");
        await ExpectShopError(() => shop.VerifyPaymentAsync(second.Id, 150m, "UNIQUE123"));
        await ExpectShopError(() => shop.VerifyPaymentAsync(second.Id, -1m, "NEGATIVE1"));
        await ExpectShopError(() => shop.VerifyPaymentAsync(second.Id, second.Total + 1m, "TOOMUCH1"));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Orders.Where(o => o.Id == second.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Cancelled));
        await ExpectShopError(() => shop.VerifyPaymentAsync(second.Id, 150m, "CANCEL123"));
        await (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == second.Id)).PaidAmount.Should().BeEqualTo(0m);
    }

    [Test]
    public async Task Coupon_LastUseConcurrentCheckoutsAllowOnlyOneAndRollbackLosingReservation()
    {
        await using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Coupons.Add(new Coupon { Code = "LASTONE", IsActive = true, DiscountType = CouponDiscountType.FixedAmount, DiscountValue = 100, UsageLimit = 1 });
            await db.SaveChangesAsync();
        }
        async Task<bool> Checkout()
        {
            await using var scope = CreateScope();
            var res = await scope.ServiceProvider.GetRequiredService<ShoppingService>().CheckoutAsync(null, Request("LASTONE"));
            return res.IsSuccess;
        }
        var results = await Task.WhenAll(Checkout(), Checkout());
        await results.Count(x => x).Should().BeEqualTo(1);
        await using var verify = CreateScope(); var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        await (await database.Coupons.SingleAsync(c => c.Code == "LASTONE")).TimesUsed.Should().BeEqualTo(1);
        await (await database.InventoryItems.SingleAsync(i => i.ProductVariantId == 1)).QuantityReserved.Should().BeEqualTo(1);
    }

    [Test]
    public async Task Inventory_LastUnitConcurrentCheckoutAllowsOnlyOneAndRefreshesCachedDetails()
    {
        await using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.InventoryItems.Where(i => i.ProductVariantId == 1).ExecuteUpdateAsync(s => s.SetProperty(i => i.QuantityOnHand, 1));
            await scope.ServiceProvider.GetRequiredService<CatalogService>().InvalidateProductCacheAsync();
            await scope.ServiceProvider.GetRequiredService<CatalogService>().DetailsAsync("apple-watch-series-10");
        }
        async Task<bool> Checkout()
        {
            await using var scope = CreateScope();
            var res = await scope.ServiceProvider.GetRequiredService<ShoppingService>().CheckoutAsync(null, Request());
            return res.IsSuccess;
        }
        await (await Task.WhenAll(Checkout(), Checkout())).Count(x => x).Should().BeEqualTo(1);
        await using var verify = CreateScope(); var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = await database.ProductVariants.Where(v => v.Id == 1).Select(v => v.Product!.Slug).SingleAsync();
        var details = await verify.ServiceProvider.GetRequiredService<CatalogService>().DetailsAsync(slug);
        await details!.Variants.Single(v => v.Id == 1).Stock.Should().BeEqualTo(0);
    }

    [Test]
    public async Task Webhook_MissingWrongAndUnconfiguredSecretsCannotVerifyPayment()
    {
        var order = await CreateOrderAsync();
        await using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.DeliverySettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.SmsWebhookSecret, "test-webhook-secret"));
            await DeliverySettingsService.InvalidateCacheAsync(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>());
        }
        using var client = AnonymousClient;
        foreach (var secret in new[] { "", "wrong" })
        {
            var response = await client.PostAsJsonAsync("/api/webhooks/mfs-sms", new { secret, trxId = "TESTTRX123", reference = order.PaymentReferenceCode, amount = 150 });
            await ((int)response.StatusCode).Should().BeEqualTo(401);
        }
        var badAmount = await client.PostAsJsonAsync("/api/webhooks/mfs-sms", new { secret = "test-webhook-secret", trxId = "TESTTRX123", reference = order.PaymentReferenceCode, amount = 0 });
        await ((int)badAmount.StatusCode).Should().BeEqualTo(400);
        await using var verify = CreateScope();
        await (await verify.ServiceProvider.GetRequiredService<AppDbContext>().Orders.SingleAsync(o => o.Id == order.Id)).PaidAmount.Should().BeEqualTo(0m);
    }

    [Test]
    public async Task Review_SubmittedByGuestIsPendingAndCannotClaimVerifiedPurchase()
    {
        using var client = AnonymousClient;
        var response = await client.PostAsync("/products/1/reviews", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Name"] = "Guest Shopper", ["Email"] = "guest@example.test", ["Rating"] = "5", ["Comment"] = "A useful and detailed customer review.", ["IsVerifiedPurchase"] = "true" }));
        await response.IsSuccessStatusCode.Should().BeTrue();
        await using var scope = CreateScope();
        var review = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductReviews.SingleAsync(r => r.CustomerName == "Guest Shopper");
        await review.IsApproved.Should().BeFalse(); await review.IsVerifiedPurchase.Should().BeFalse();
    }

    [Test]
    public async Task Review_DeliveredCustomerReceivesVerifiedBadgeButStillRequiresModeration()
    {
        var order = await CreateOrderAsync(2);
        await using (var scope = CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Where(o => o.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Delivered));
        using var client = CustomerClient;
        await client.PostAsync("/products/1/reviews", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Name"] = "Actual Buyer", ["Rating"] = "4", ["Comment"] = "This review follows a delivered purchase." }));
        await using var verify = CreateScope();
        var review = await verify.ServiceProvider.GetRequiredService<AppDbContext>().ProductReviews.SingleAsync(r => r.CustomerName == "Actual Buyer");
        await review.IsVerifiedPurchase.Should().BeTrue(); await review.IsApproved.Should().BeFalse();
    }

    [Test]
    public async Task Webhook_ValidPaymentIsReplaySafeAndUnconfiguredWebhookIsDisabled()
    {
        var order = await CreateOrderAsync();
        await using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.DeliverySettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.SmsWebhookSecret, "safe-webhook-secret"));
            await DeliverySettingsService.InvalidateCacheAsync(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>());
        }
        using var client = AnonymousClient;
        var payload = new { secret = "safe-webhook-secret", trxId = "WEBHOOK123", reference = order.PaymentReferenceCode, amount = 150 };
        await ((int)(await client.PostAsJsonAsync("/api/webhooks/mfs-sms", payload)).StatusCode).Should().BeEqualTo(200);
        await ((int)(await client.PostAsJsonAsync("/api/webhooks/mfs-sms", payload)).StatusCode).Should().BeEqualTo(200);
        await using var verify = CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        await (await database.OrderTimelineEvents.CountAsync(x => x.OrderId == order.Id && x.Title == "Payment Verified")).Should().BeEqualTo(1);
        await database.DeliverySettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.SmsWebhookSecret, (string?)null));
        await DeliverySettingsService.InvalidateCacheAsync(verify.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>());
        await ((int)(await client.PostAsJsonAsync("/api/webhooks/mfs-sms", payload)).StatusCode).Should().BeEqualTo(503);
    }

    [Test]
    public async Task Tracking_CompletePhoneWorksButSuffixDoesNotRevealOrder()
    {
        var order = await CreateOrderAsync(); using var client = AnonymousClient;
        var partial = await client.GetStringAsync($"/track-order?id={order.Id}&phone=345678");
        await partial.Contains("No Order Found").Should().BeTrue();
        var full = await client.GetStringAsync($"/track-order?id={order.Id}&phone=%2B8801712345678");
        await full.Contains("Test Customer").Should().BeTrue();
    }

    [Test]
    public async Task StockAdmin_ProvidesMailConfigurationAndRequestVisibility()
    {
        using var client = AdminClient;
        var response = await client.GetAsync("/admin/stock-notifications");
        await ((int)response.StatusCode).Should().BeEqualTo(200);
        var html = await response.Content.ReadAsStringAsync();
        await html.Contains("Email is not configured").Should().BeTrue();
        await html.Contains("No requests yet").Should().BeTrue();
    }

    [Test]
    public async Task AdminSettings_SecretPersistsAndInvalidatesHybridCache()
    {
        await using var scope = CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<DeliverySettingsService>();
        await settings.GetSettingsAsync();
        using var client = AdminClient;
        var html = await client.GetStringAsync("/admin/settings/delivery");
        var name = System.Text.RegularExpressions.Regex.Match(html, "name=\"([^\"]*SmsWebhookSecret)\"").Groups[1].Value;
        await string.IsNullOrEmpty(name).Should().BeFalse();
        var fields = new Dictionary<string, string>();
        foreach (System.Text.RegularExpressions.Match input in System.Text.RegularExpressions.Regex.Matches(html, "<input[^>]*>"))
        {
            var fieldName = System.Text.RegularExpressions.Regex.Match(input.Value, "name=\"([^\"]+)\"").Groups[1].Value;
            var value = System.Text.RegularExpressions.Regex.Match(input.Value, "value=\"([^\"]*)\"").Groups[1].Value;
            if (fieldName.Length > 0) fields[fieldName] = WebUtility.HtmlDecode(value);
        }
        fields["_handler"] = "delivery-settings";
        fields[name] = "new-unique-webhook-secret-for-tests";
        var response = await client.PostAsync("/admin/settings/delivery", new FormUrlEncodedContent(fields));
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Settings POST {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var updated = await settings.GetSettingsAsync();
        await updated.SmsWebhookSecret.Should().BeEqualTo("new-unique-webhook-secret-for-tests");
    }

    [Test]
    public async Task Webhook_ParsesFormSmsAmountsInvariantlyAndRejectsMalformedJson()
    {
        var order = await CreateOrderAsync();
        await using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.DeliverySettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.SmsWebhookSecret, "parser-test-secret"));
            await DeliverySettingsService.InvalidateCacheAsync(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>());
        }
        using var client = AnonymousClient;
        var invalid = await client.PostAsync("/api/webhooks/mfs-sms", new StringContent("{broken", System.Text.Encoding.UTF8, "application/json"));
        await ((int)invalid.StatusCode).Should().BeEqualTo(400);
        await (await invalid.Content.ReadAsStringAsync()).Contains("JsonException").Should().BeFalse();
        var response = await client.PostAsync("/api/webhooks/mfs-sms", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = "parser-test-secret",
            ["sms"] = $"Received Tk 1,250.50 Reference: {order.PaymentReferenceCode} TrxID: FORM123456"
        }));
        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await using var verify = CreateScope();
        var persisted = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        await persisted.PaidAmount.Should().BeEqualTo(1250.50m);
        await persisted.TransactionId.Should().BeEqualTo("FORM123456");
    }

    [Test]
    public async Task ExternalLogin_RejectsUnconfiguredProviderAndSanitizesProviderErrors()
    {
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await ((int)(await client.GetAsync("/account/external-login?provider=Unknown&returnUrl=https://evil.example")).StatusCode).Should().BeEqualTo(400);
        var error = await client.GetAsync("/account/external-login-callback?remoteError=private-provider-error");
        await ((int)error.StatusCode).Should().BeEqualTo(302);
        await error.Headers.Location!.OriginalString.StartsWith("/account/login?").Should().BeTrue();
        await error.Headers.Location.OriginalString.Contains("private-provider-error").Should().BeFalse();
    }

    private static async Task ExpectShopError(Func<Task<Result<Order>>> action)
    {
        var result = await action();
        await result.IsFailure.Should().BeTrue();
    }
}
