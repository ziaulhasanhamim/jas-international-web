using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class AdminCategoryIntegrationTests : JasIntegrationTestBase
{

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

    [Test]
    public async Task DeleteCategoryAsync_WithChildCategories_SafelyReparentsAndDeletesPermanently()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var parentCat = new Category { Name = "Parent Cat " + Guid.NewGuid().ToString("N")[..6], Slug = "parent-cat-" + Guid.NewGuid().ToString("N")[..6] };
        db.Categories.Add(parentCat);
        await db.SaveChangesAsync();

        var childCat = new Category { Name = "Child Cat " + Guid.NewGuid().ToString("N")[..6], Slug = "child-cat-" + Guid.NewGuid().ToString("N")[..6], ParentCategoryId = parentCat.Id };
        db.Categories.Add(childCat);
        await db.SaveChangesAsync();

        // Permanent delete parent category
        await admin.DeleteCategoryAsync(parentCat.Id, permanent: true);

        // Verify parent is removed
        var checkParent = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == parentCat.Id);
        await checkParent.Should().BeNull();

        // Verify child category was reparented to null and was not blocked by FK constraint
        var checkChild = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == childCat.Id);
        await checkChild.Should().NotBeNull();
        await checkChild!.ParentCategoryId.Should().BeNull();
    }

    [Test]
    public async Task DeleteCategoryAsync_AlreadyHiddenCategory_DeletesPermanently()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cat = new Category
        {
            Name = "Archived Cat " + Guid.NewGuid().ToString("N")[..6],
            Slug = "archived-cat-" + Guid.NewGuid().ToString("N")[..6],
            IsDeleted = true,
            DeletedAt = DateTimeOffset.UtcNow
        };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        // Calling standard delete on an already hidden category should permanently delete it
        await admin.DeleteCategoryAsync(cat.Id, permanent: false);

        var checkCat = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cat.Id);
        await checkCat.Should().BeNull();
    }

    [Test]
    public async Task CategoriesAdmin_Query_ExecutesSuccessfullyOnDatabase()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var q = db.Categories.AsNoTracking();
        var categoriesQuery = q.OrderBy(c => c.ParentCategoryId == null ? 0 : 1)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Slug,
                c.Icon,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.Name : null,
                ProductCount = c.Products.Count(p => !p.IsDeleted),
                IsFeatured = db.CategoryPlacements.Any(p => p.CategoryId == c.Id && p.PlacementType == CategoryPlacementType.HomepageFeatured && p.IsActive),
                c.IsDeleted
            });

        var results = await categoriesQuery.Take(15).ToListAsync();
        await results.Should().NotBeNull();
    }

    [Test]
    public async Task GetCategoriesAdminPage_WithPage2QueryParam_ReturnsOk()
    {
        var response = await AdminClient.GetAsync("/admin/categories?page=2");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"HTTP {(int)response.StatusCode}: {body}");
        }
        await response.IsSuccessStatusCode.Should().BeTrue();
    }
}
