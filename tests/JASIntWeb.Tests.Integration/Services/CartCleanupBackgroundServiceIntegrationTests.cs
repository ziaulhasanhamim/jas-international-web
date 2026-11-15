using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace JASIntWeb.Tests.Integration.Services;

public class CartCleanupBackgroundServiceIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task CleanupStaleCartsAsync_DeletesConvertedAndStaleCarts_PreservesActiveRecentCarts()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        // 1. Converted cart (should be deleted)
        var convertedCart = new Cart
        {
            Token = Guid.CreateVersion7(),
            Status = CartStatus.Converted,
            LastActivityAt = DateTimeOffset.UtcNow
        };
        convertedCart.Items.Add(new CartItem { ProductVariantId = 1, Quantity = 2 });

        // 2. Inactive >30 days cart (should be deleted)
        var staleCart = new Cart
        {
            Token = Guid.CreateVersion7(),
            Status = CartStatus.Active,
            LastActivityAt = DateTimeOffset.UtcNow.AddDays(-32)
        };
        staleCart.Items.Add(new CartItem { ProductVariantId = 1, Quantity = 1 });

        // 3. Active recent cart (must be preserved)
        var activeRecentCart = new Cart
        {
            Token = Guid.CreateVersion7(),
            Status = CartStatus.Active,
            LastActivityAt = DateTimeOffset.UtcNow
        };
        activeRecentCart.Items.Add(new CartItem { ProductVariantId = 1, Quantity = 3 });

        db.Carts.AddRange(convertedCart, staleCart, activeRecentCart);
        await db.SaveChangesAsync();

        var cleanupService = new CartCleanupBackgroundService(dbFactory, NullLogger<CartCleanupBackgroundService>.Instance);
        var deletedCount = await cleanupService.CleanupStaleCartsAsync();

        await (deletedCount >= 2).Should().BeTrue();

        // Verify active recent cart still exists in PostgreSQL
        var freshInDb = await db.Carts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == activeRecentCart.Id);
        await freshInDb.Should().NotBeNull();
        await freshInDb!.Status.Should().BeEqualTo(CartStatus.Active);

        // Verify converted and stale carts are deleted
        var convertedInDb = await db.Carts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == convertedCart.Id);
        var staleInDb = await db.Carts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == staleCart.Id);
        await convertedInDb.Should().BeNull();
        await staleInDb.Should().BeNull();
    }
}
