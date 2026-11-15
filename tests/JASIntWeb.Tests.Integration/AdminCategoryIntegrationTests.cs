using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

[NotInParallel("web-integration")]
public class AdminCategoryIntegrationTests
{
    private static readonly WebApplicationFactory<Program> Factory = new();

    [Test]
    public async Task Categories_ExtraAutomationCategories_AreCleanedUpOnStartup()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure no active category with "automation" remains active
        var activeAutomations = await db.Categories
            .Where(c => !c.IsDeleted && (
                c.Name.ToLower().Contains("automation") ||
                c.Slug.ToLower().Contains("automation")
            ))
            .ToListAsync();

        await activeAutomations.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task ToggleCategoryFeaturedAsync_TogglesPlacementStatus()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cat = await db.Categories.FirstOrDefaultAsync(c => !c.IsDeleted);
        if (cat == null)
        {
            cat = new Category { Name = "Test Category For Placement", Slug = "test-cat-placement" };
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
        }

        var placementBefore = await db.CategoryPlacements.FirstOrDefaultAsync(p => p.CategoryId == cat.Id && p.PlacementType == CategoryPlacementType.HomepageFeatured);
        var wasActive = placementBefore?.IsActive == true;

        await admin.ToggleCategoryFeaturedAsync(cat.Id);

        var placementAfter = await db.CategoryPlacements.AsNoTracking().FirstOrDefaultAsync(p => p.CategoryId == cat.Id && p.PlacementType == CategoryPlacementType.HomepageFeatured);
        await placementAfter.Should().NotBeNull();
        await placementAfter!.IsActive.Should().BeEqualTo(!wasActive);
    }

    [Test]
    public async Task DeleteCategoryAsync_SoftDeletesCategoryAndDeactivatesPlacements()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cat = new Category { Name = "Category To Delete", Slug = "cat-to-delete-" + Guid.NewGuid().ToString("N")[..6] };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        var placement = new CategoryPlacement
        {
            CategoryId = cat.Id,
            PlacementType = CategoryPlacementType.HomepageFeatured,
            SortOrder = 99,
            IsActive = true
        };
        db.CategoryPlacements.Add(placement);
        await db.SaveChangesAsync();

        await admin.DeleteCategoryAsync(cat.Id);

        var savedCat = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cat.Id);
        await savedCat.Should().NotBeNull();
        await savedCat!.IsDeleted.Should().BeTrue();

        var savedPlacement = await db.CategoryPlacements.AsNoTracking().SingleOrDefaultAsync(p => p.Id == placement.Id);
        await savedPlacement.Should().NotBeNull();
        await savedPlacement!.IsActive.Should().BeFalse();
    }
}
