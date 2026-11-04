using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

[NotInParallel("web-integration")]
public class AdminProductServiceIntegrationTests
{
    private static readonly WebApplicationFactory<Program> Factory = new();

    [Test]
    public async Task SaveProductAsync_WhenSlugIsEmpty_AutoGeneratesSlugFromName()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure a category exists
        var category = await db.Categories.FirstOrDefaultAsync(c => !c.IsDeleted);
        if (category == null)
        {
            category = new JASIntWeb.Persistence.Entities.Category { Name = "Test Audio", Slug = "test-audio" };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var form = new ProductEditForm
        {
            Name = $"Sony WH-1000XM6 Wireless Noise Cancelling Headphones {suffix}",
            Slug = "", // Empty slug to test auto-generation
            CategoryId = category.Id,
            Description = "Next-gen flagship noise cancelling headphones.",
            Details = "<h3>Key Features</h3><ul><li>Dual processors</li></ul>",
            Image = "/images/products/xm6.jpg",
            Hidden = false
        };

        var productId = await admin.SaveProductAsync(form);
        await productId.Should().BeGreaterThan(0);

        var saved = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId);
        await saved.Should().NotBeNull();
        await saved!.Slug.Should().BeEqualTo($"sony-wh-1000xm6-wireless-noise-cancelling-headphones-{suffix}");
        await saved.Name.Should().BeEqualTo(form.Name);
    }

    [Test]
    public async Task SaveProductAsync_WithBasePriceAndStock_CreatesDefaultVariantWithAutoSkuAndBarcode()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = await db.Categories.FirstOrDefaultAsync(c => !c.IsDeleted);
        if (category == null)
        {
            category = new JASIntWeb.Persistence.Entities.Category { Name = "Gadgets", Slug = "gadgets" };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var form = new ProductEditForm
        {
            Name = $"Anker 737 Power Bank {suffix}",
            Slug = "",
            CategoryId = category.Id,
            Description = "Ultra-powerful two-way charging power bank.",
            Details = "High capacity 140W fast charging.",
            Image = "/images/products/anker-737.jpg",
            BasePrice = 14500m,
            CostPrice = 12000m,
            InitialStock = 18,
            Hidden = false
        };

        var productId = await admin.SaveProductAsync(form);
        await productId.Should().BeGreaterThan(0);

        var variant = await db.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.ProductId == productId);
        await variant.Should().NotBeNull();
        await variant!.SellingPrice.Should().BeEqualTo(14500m);
        await variant.CostPrice.Should().BeEqualTo(12000m);
        await variant.SKU.Should().NotBeEmpty();
        await variant.Barcode.Should().NotBeEmpty();
        await variant.IsDefault.Should().BeTrue();
        await variant.IsActive.Should().BeTrue();

        var inventory = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.ProductVariantId == variant.Id);
        await inventory.Should().NotBeNull();
        await inventory!.QuantityOnHand.Should().BeEqualTo(18);
    }

    [Test]
    public async Task SaveCategoryAsync_WhenSlugIsEmpty_AutoGeneratesCategorySlug()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var form = new CategoryEditForm
        {
            Name = $"Smart Home Automation {suffix}",
            Slug = "",
            Icon = "home",
            Featured = false,
            Hidden = false
        };

        await admin.SaveCategoryAsync(form);

        var saved = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Name == $"Smart Home Automation {suffix}");
        await saved.Should().NotBeNull();
        await saved!.Slug.Should().BeEqualTo($"smart-home-automation-{suffix}");
    }

    [Test]
    public async Task SaveBrandAsync_WhenSlugIsEmpty_AutoGeneratesBrandSlug()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var form = new BrandEditForm
        {
            Name = $"Nothing Technologies {suffix}",
            Slug = "",
            Description = "London-based tech company.",
            IsFeatured = true,
            SortOrder = 10,
            Hidden = false
        };

        await admin.SaveBrandAsync(form);

        var saved = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.Name == $"Nothing Technologies {suffix}");
        await saved.Should().NotBeNull();
        await saved!.Slug.Should().BeEqualTo($"nothing-technologies-{suffix}");
    }
}
