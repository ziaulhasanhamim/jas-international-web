using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using JASIntWeb.Services.MediaMigration;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class MediaMigrationIntegrationTests : JasIntegrationTestBase
{
    private const string Old = "https://res.cloudinary.com/store/image/upload/f_auto,q_auto/v123/jas-storefront/product.png";
    private const string New = "https://media.example.test/jas-media/images/static/0123456789abcdef0123456789abcdef.webp";
    private async Task<MediaMigrationManifest> Manifest(AppDbContext db)
    {
        var service = new MediaReferenceMigration(db);
        return new()
        {
            DatabaseFingerprint = service.Fingerprint(), DestinationBaseUrl = "https://media.example.test/jas-media",
            Assets = [new() { SourceUrl = LegacyMediaUrls.Canonical(Old), Identity = new('a', 64), DestinationUrl = New, VerifiedObjects = new() { [New] = "verified-fixture" } }],
            References = (await service.ScanAsync()).Select(reference => reference with { After = LegacyMediaUrls.Replace(reference.Before, new Dictionary<string, string> { [LegacyMediaUrls.Canonical(Old)] = New }) }).ToList()
        };
    }
    [Test]
    public async Task Migration_PreservesInventoryReservationsAndOrderSnapshotsAcrossReplayAndRollback()
    {
        await using var scope = CreateScope();
        var shopping = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        var order = await shopping.CheckoutAsync(null, new CheckoutRequest
        {
            RequestId = Guid.NewGuid(), CustomerName = "Migration fixture", PhoneNumber = "01712345678", District = "Khulna", City = "Khulna",
            Address = "Fixture address", PaymentMethod = "COD", Items = [new(1, 1)]
        });
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(p => p.MainImageUrl, Old).SetProperty(p => p.Details, $"<h2>Specs</h2><img src=\"{Old}\">"));
        var revision = await db.Products.Where(p => p.Id == 1).Select(p => p.CatalogRevision).SingleAsync();
        var inventory = await db.InventoryItems.AsNoTracking().SingleAsync(i => i.ProductVariantId == 1);
        var snapshot = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == order.OrderId);
        var manifest = await Manifest(db);
        var migration = new MediaReferenceMigration(db);
        await (await migration.ApplyAsync(manifest, false)).Should().BeEqualTo(2);
        await (await migration.ApplyAsync(manifest, false)).Should().BeEqualTo(0);
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == 1);
        await product.MainImageUrl.Should().BeEqualTo(New);
        await product.Details.Should().BeEqualTo($"<h2>Specs</h2><img src=\"{New}\">");
        await product.CatalogRevision.Should().BeEqualTo(revision + 1);
        var currentInventory = await db.InventoryItems.AsNoTracking().SingleAsync(i => i.Id == inventory.Id);
        await currentInventory.QuantityOnHand.Should().BeEqualTo(inventory.QuantityOnHand);
        await currentInventory.QuantityReserved.Should().BeEqualTo(inventory.QuantityReserved);
        var currentSnapshot = await db.OrderItems.AsNoTracking().SingleAsync(i => i.Id == snapshot.Id);
        await currentSnapshot.ProductVariantId.Should().BeEqualTo(snapshot.ProductVariantId);
        await currentSnapshot.ProductName.Should().BeEqualTo(snapshot.ProductName);
        await currentSnapshot.UnitPrice.Should().BeEqualTo(snapshot.UnitPrice);
        await (await migration.ApplyAsync(manifest, true)).Should().BeEqualTo(2);
        await (await db.Products.AsNoTracking().SingleAsync(p => p.Id == 1)).MainImageUrl.Should().BeEqualTo(Old);
    }
    [Test]
    public async Task Migration_ConcurrentEditAbortsWholeTransactionAndWrongDatabaseIsRejected()
    {
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(p => p.MainImageUrl, Old).SetProperty(p => p.Details, Old));
        var manifest = await Manifest(db);
        await db.Products.Where(p => p.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(p => p.MainImageUrl, "https://another.example.test/new.webp"));
        var migration = new MediaReferenceMigration(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => migration.ApplyAsync(manifest, false));
        await (await db.Products.AsNoTracking().SingleAsync(p => p.Id == 1)).Details.Should().BeEqualTo(Old);
        manifest.DatabaseFingerprint = "wrong-target";
        await Assert.ThrowsAsync<InvalidOperationException>(() => migration.ApplyAsync(manifest, false));
    }
    [Test]
    public async Task Migration_UnverifiedAssetsAndNonMediaFieldsAreRejected()
    {
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(p => p.MainImageUrl, Old));
        var manifest = await Manifest(db); var migration = new MediaReferenceMigration(db);
        manifest.Assets[0].VerifiedObjects.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => migration.ApplyAsync(manifest, false));
        manifest.Assets[0].VerifiedObjects[New] = "fixture";
        manifest.References = [new("AspNetUsers", "Email", 1, null, "old", "new")];
        await Assert.ThrowsAsync<InvalidDataException>(() => migration.ApplyAsync(manifest, false));
    }
}
