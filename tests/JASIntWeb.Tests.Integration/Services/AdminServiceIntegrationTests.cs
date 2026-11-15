using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration.Services;

public class AdminServiceIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task ToggleCategoryFeaturedAsync_TogglesPlacementInDatabase()
    {
        await using var scope = CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var catId = 1; // Power & Chargers (featured = true in seed)
        await admin.ToggleCategoryFeaturedAsync(catId);

        var placement = await db.CategoryPlacements.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CategoryId == catId && p.PlacementType == CategoryPlacementType.HomepageFeatured);

        await placement.Should().NotBeNull();
        await placement!.IsActive.Should().BeFalse();

        // Toggle again
        await admin.ToggleCategoryFeaturedAsync(catId);
        var placement2 = await db.CategoryPlacements.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CategoryId == catId && p.PlacementType == CategoryPlacementType.HomepageFeatured);

        await placement2!.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task DeleteCategoryAsync_SoftDeletesCategory()
    {
        await using var scope = CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cat = new Category { Name = "Temporary Category", Slug = "temp-cat-test" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        await admin.DeleteCategoryAsync(cat.Id);

        var inDb = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cat.Id);
        await inDb.Should().NotBeNull();
        await inDb!.IsDeleted.Should().BeTrue();
    }

    [Test]
    public async Task SaveBrandAsync_PersistsBrandToDatabase()
    {
        await using var scope = CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var brandForm = new BrandEditForm
        {
            Id = 0,
            Name = "Sony Audio",
            Slug = "sony-audio",
            LogoUrl = "https://example.com/sony.png",
            Description = "World leading audio brand",
            IsFeatured = true,
            SortOrder = 5
        };

        await admin.SaveBrandAsync(brandForm);

        var inDb = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.Name == "Sony Audio");
        await inDb.Should().NotBeNull();
        await inDb!.Slug.Should().BeEqualTo("sony-audio");
        await inDb.LogoUrl.Should().BeEqualTo("https://example.com/sony.png");
        await inDb.IsFeatured.Should().BeTrue();
    }

    [Test]
    public async Task DeleteBrandAsync_SoftDeletesBrand()
    {
        await using var scope = CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var brand = new Brand { Name = "Brand To Delete", Slug = "brand-to-delete" };
        db.Brands.Add(brand);
        await db.SaveChangesAsync();

        await admin.DeleteBrandAsync(brand.Id);

        var inDb = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.Id == brand.Id);
        await inDb.Should().NotBeNull();
        await inDb!.IsDeleted.Should().BeTrue();
    }
}
