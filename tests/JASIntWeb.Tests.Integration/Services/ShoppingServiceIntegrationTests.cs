using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration.Services;

public class ShoppingServiceIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task ChangeAsync_AddsItemToCart_PersistsInDatabase()
    {
        await using var scope = CreateScope();
        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = Guid.CreateVersion7();
        var cartDto = await shop.ChangeAsync(token, null, new CartLineRequest(1, 2), add: true);

        await cartDto.Should().NotBeNull();
        await cartDto.Items.Count.Should().BeEqualTo(1);
        await cartDto.Items[0].VariantId.Should().BeEqualTo(1);
        await cartDto.Items[0].Quantity.Should().BeEqualTo(2);

        // Verify directly in PostgreSQL container
        var cartInDb = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Token == token);
        await cartInDb.Should().NotBeNull();
        await cartInDb!.Status.Should().BeEqualTo(CartStatus.Active);
        await cartInDb.Items.Count.Should().BeEqualTo(1);
        await cartInDb.Items.First().Quantity.Should().BeEqualTo(2);
    }

    [Test]
    public async Task ChangeAsync_QuantityZero_RemovesItemFromCart()
    {
        await using var scope = CreateScope();
        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = Guid.CreateVersion7();
        await shop.ChangeAsync(token, null, new CartLineRequest(1, 3), add: true);

        // Update quantity to 0
        var updated = await shop.ChangeAsync(token, null, new CartLineRequest(1, 0), add: false);
        await updated.Items.Count.Should().BeEqualTo(0);

        var cartInDb = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Token == token);
        await cartInDb.Should().NotBeNull();
        await cartInDb!.Items.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task MergeAsync_MergesGuestItemsIntoAuthenticatedUserCart()
    {
        await using var scope = CreateScope();
        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userId = 2; // Customer user from base seed
        var mergeToken = Guid.CreateVersion7();

        var mergeRequest = new CartMergeRequest(mergeToken, [new CartLineRequest(1, 2)]);
        var userCart = await shop.MergeAsync(userId, mergeRequest);

        await userCart.Should().NotBeNull();
        await userCart.Items.Count.Should().BeEqualTo(1);
        await userCart.Items[0].Quantity.Should().BeEqualTo(2);

        // Verify merge receipt is recorded to prevent replay
        var receipt = await db.CartMergeReceipts.FirstOrDefaultAsync(r => r.UserId == userId && r.Token == mergeToken);
        await receipt.Should().NotBeNull();
    }

    [Test]
    public async Task CheckoutAsync_CreatesOrder_ReservesInventory_AndConvertsCart()
    {
        await using var scope = CreateScope();
        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = Guid.CreateVersion7();
        await shop.ChangeAsync(token, null, new CartLineRequest(1, 2), add: true);

        var inventoryBefore = await db.InventoryItems.FirstAsync(i => i.ProductVariantId == 1);
        var initialReserved = inventoryBefore.QuantityReserved;

        var checkoutRequest = new CheckoutRequest
        {
            RequestId = Guid.NewGuid(),
            CartToken = token,
            CustomerName = "Rahim Ahmed",
            PhoneNumber = "01712345678",
            Email = "rahim@example.com",
            Division = "Khulna",
            District = "Khulna",
            Thana = "Khulna Sadar",
            City = "Khulna",
            Address = "House 12, Road 4, Sonadanga, Khulna",
            PaymentMethod = "COD",
            Items = [new CartLineRequest(1, 2)],
            ExpectedTotal = 29060m // (14,500 * 2) + 60 delivery fee inside Khulna
        };

        var result = await shop.CheckoutAsync(null, checkoutRequest);

        await result.Should().NotBeNull();
        await result.OrderId.Should().BeGreaterThan(0);
        await result.Total.Should().BeEqualTo(29060m);

        // Verify Order in DB
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryAddress)
            .FirstOrDefaultAsync(o => o.Id == result.OrderId);
        await order.Should().NotBeNull();
        await order!.DeliveryAddress!.CustomerName.Should().BeEqualTo("Rahim Ahmed");
        await order.Status.Should().BeEqualTo(OrderStatus.Pending);
        await order.Items.Count.Should().BeEqualTo(1);
        await order.Items.First().Quantity.Should().BeEqualTo(2);

        // Verify Inventory Reservation
        var inventoryAfter = await db.InventoryItems.FirstAsync(i => i.ProductVariantId == 1);
        await inventoryAfter.QuantityReserved.Should().BeEqualTo(initialReserved + 2);

        // Verify Cart is Converted and items cleared
        var cartInDb = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Token == token);
        await cartInDb.Should().NotBeNull();
        await cartInDb!.Status.Should().BeEqualTo(CartStatus.Converted);
        await cartInDb.Items.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task CheckoutAsync_WhenQuantityExceedsAvailableStock_ThrowsShopException()
    {
        await using var scope = CreateScope();
        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = Guid.CreateVersion7();
        await shop.ChangeAsync(token, null, new CartLineRequest(1, 2), add: true);

        // Simulate stock depletion before checkout completes
        var inv = await db.InventoryItems.FirstAsync(i => i.ProductVariantId == 1);
        inv.QuantityOnHand = 1; // only 1 left, but user cart has 2
        inv.QuantityReserved = 0;
        await db.SaveChangesAsync();

        var checkoutRequest = new CheckoutRequest
        {
            RequestId = Guid.NewGuid(),
            CartToken = token,
            CustomerName = "Over Stock Customer",
            PhoneNumber = "01712345678",
            District = "Khulna",
            Address = "Test Address, Khulna",
            PaymentMethod = "COD"
        };

        await Assert.ThrowsAsync<ShopException>(() => shop.CheckoutAsync(null, checkoutRequest));
    }
}
