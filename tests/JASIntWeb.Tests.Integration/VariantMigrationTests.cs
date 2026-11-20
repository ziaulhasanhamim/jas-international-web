using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
namespace JASIntWeb.Tests.Integration;

public class VariantMigrationTests
{
    [Test]
    public async Task EditorV2Migration_PreservesIdentitiesSelectionsReservationsAndOrderSnapshots()
    {
        await using var database = new PostgreSqlBuilder("postgres:16-alpine").Build(); await database.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new AppDbContext(options); await db.Database.MigrateAsync();
        var category = new Category { Name = "Audio", Slug = "audio" };
        var product = new Product { Name = "Historic product", Slug = "historic", Details = "Details", MainImageUrl = "/jas-logo.png", CategoryId = 0, Category = category, CompareAtPrice = 900 };
        var definition = new AttributeDefinition { Name = "Color", Code = "color", Options = [new() { Value = "Midnight Black", Code = "midnight-black" }] };
        db.AddRange(product, definition); await db.SaveChangesAsync();
        var binding = new ProductAttributeDefinition { ProductId = product.Id, AttributeDefinitionId = definition.Id };
        db.Add(binding); await db.SaveChangesAsync();
        var value = new ProductAttributeOption { ProductId = product.Id, AttributeDefinitionId = definition.Id, ProductAttributeDefinitionId = binding.Id, AttributeOptionId = definition.Options.Single().Id, Value = "Midnight Black", Code = "midnight-black", NormalizedValue = "MIDNIGHT BLACK" };
        db.Add(value); await db.SaveChangesAsync();
        var variant = new ProductVariant { ProductId = product.Id, SKU = "HISTORIC-SKU", Barcode = "12345670", SellingPrice = 700, CostPrice = 400, CompareAtPrice = 900, OptionSignature = value.Id.ToString(), Options = [new() { ProductId = product.Id, ProductAttributeDefinitionId = binding.Id, ProductAttributeOptionId = value.Id }] };
        db.Add(variant); await db.SaveChangesAsync();
        var inventory = new InventoryItem { ProductVariantId = variant.Id, QuantityOnHand = 8, QuantityReserved = 3 };
        var order = new Order
        {
            Status = OrderStatus.Processing,
            DeliveryAddressId = 0,
            DeliveryAddress = new() { District = "Khulna", City = "Khulna", PhoneNumber = "01712345678", CustomerName = "Migration fixture", FullAddress = "Isolated test address" },
            Subtotal = 2100,
            DeliveryCharge = 50,
            Total = 2150,
            Items = [new() { ProductVariantId = variant.Id, ProductName = "Snapshot name", VariantName = "Color: Midnight Black", UnitPrice = 700, UnitCost = 400, Quantity = 3, LineTotal = 2100 }]
        };
        db.AddRange(inventory, order); await db.SaveChangesAsync();
        var variantId = variant.Id; var optionId = value.Id; var orderId = order.Id; var itemId = order.Items.Single().Id;
        var migrator = db.GetService<IMigrator>(); var previous = db.Database.GetMigrations().Reverse().Skip(1).First();
        db.ChangeTracker.Clear(); await migrator.MigrateAsync(previous);
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        var restored = await db.ProductVariants.Include(v => v.Options).SingleAsync(v => v.Id == variantId);
        await restored.NormalizedSku.Should().BeEqualTo("HISTORIC-SKU"); await restored.SKU.Should().BeEqualTo("HISTORIC-SKU"); await restored.Barcode.Should().BeEqualTo("12345670"); await restored.SellingPrice.Should().BeEqualTo(700); await restored.CompareAtPrice.Should().BeEqualTo(900);
        await restored.OptionSignature.Should().BeEqualTo(optionId.ToString()); await restored.Options.Single().ProductAttributeOptionId.Should().BeEqualTo(optionId);
        var local = await db.ProductAttributeOptions.SingleAsync(o => o.Id == optionId); await local.MetadataJson.Should().BeEqualTo("{}"); await local.IsRetired.Should().BeFalse();
        await local.Value.Should().BeEqualTo("Midnight Black"); await local.Code.Should().BeEqualTo("midnight-black"); await local.NormalizedValue.Should().BeEqualTo("MIDNIGHT BLACK");
        var stock = await db.InventoryItems.SingleAsync(i => i.ProductVariantId == variantId); await (stock.Version > 0).Should().BeTrue(); await stock.QuantityOnHand.Should().BeEqualTo(8); await stock.QuantityReserved.Should().BeEqualTo(3);
        var history = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == orderId); await history.Total.Should().BeEqualTo(2150); await history.Status.Should().BeEqualTo(OrderStatus.Processing);
        await history.Items.Single().Id.Should().BeEqualTo(itemId); await history.Items.Single().ProductName.Should().BeEqualTo("Snapshot name"); await history.Items.Single().VariantName.Should().BeEqualTo("Color: Midnight Black"); await history.Items.Single().UnitCost.Should().BeEqualTo(400);
    }
    [Test]
    public async Task EditorV2Migration_StopsOnLegacySkuCollisionsUntilExplicitlyResolved()
    {
        await using var database = new PostgreSqlBuilder("postgres:16-alpine").Build(); await database.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new AppDbContext(options); await db.Database.MigrateAsync();
        var category = new Category { Name = "Migration", Slug = "migration" };
        var product = new Product { Name = "Fixture", Slug = "fixture", Details = "Details", MainImageUrl = "/jas-logo.png", CategoryId = 0, Category = category };
        db.Add(product); await db.SaveChangesAsync();
        var first = new ProductVariant { ProductId = product.Id, SKU = "LEGACY-SKU", OptionSignature = "one" };
        var second = new ProductVariant { ProductId = product.Id, SKU = "SECOND-SKU", OptionSignature = "two", IsDefault = false };
        db.AddRange(first, second); await db.SaveChangesAsync(); var secondId = second.Id;
        var allMigrations = db.Database.GetMigrations().ToList();
        var editorV2Index = allMigrations.FindIndex(m => m.Contains("ProductEditorV2"));
        var priorMigration = editorV2Index > 0 ? allMigrations[editorV2Index - 1] : allMigrations.First();
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(priorMigration);
        await db.Database.ExecuteSqlAsync($"UPDATE \"ProductVariants\" SET \"SKU\" = ' legacy-sku ' WHERE \"Id\" = {secondId}");
        var rejected = false; try { await migrator.MigrateAsync(); } catch (Npgsql.PostgresException e) { rejected = e.Message.Contains("SKU normalization collision"); }
        await rejected.Should().BeTrue();
        await db.Database.ExecuteSqlAsync($"UPDATE \"ProductVariants\" SET \"SKU\" = 'EXPLICIT-RESOLUTION' WHERE \"Id\" = {secondId}");
        await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        await (await db.ProductVariants.SingleAsync(v => v.Id == secondId)).SKU.Should().BeEqualTo("EXPLICIT-RESOLUTION");
        await (await db.ProductVariants.SingleAsync(v => v.Id == first.Id)).SKU.Should().BeEqualTo("LEGACY-SKU");
    }
}
