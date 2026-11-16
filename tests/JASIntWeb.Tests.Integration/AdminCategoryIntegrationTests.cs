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

        await admin.DeleteCategoryAsync(cat.Id, permanent: false);

        var savedCat = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cat.Id);
        await savedCat.Should().NotBeNull();
        await savedCat!.IsDeleted.Should().BeTrue();

        var savedPlacement = await db.CategoryPlacements.AsNoTracking().SingleOrDefaultAsync(p => p.Id == placement.Id);
        await savedPlacement.Should().NotBeNull();
        await savedPlacement!.IsActive.Should().BeFalse();
    }

    [Test]
    public async Task DeleteCategoryAsync_WithActiveProducts_SafelyReassignsToUncategorizedAndDeletesPermanently()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cat = new Category { Name = "Cat With Products " + Guid.NewGuid().ToString("N")[..6], Slug = "cat-with-prods-" + Guid.NewGuid().ToString("N")[..6] };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        var prod = new Product
        {
            Name = "Product In Category " + Guid.NewGuid().ToString("N")[..6],
            Slug = "prod-in-cat-" + Guid.NewGuid().ToString("N")[..6],
            Details = "Test Details",
            MainImageUrl = "/jas-logo.png",
            CategoryId = cat.Id,
            IsDeleted = false
        };
        db.Products.Add(prod);
        await db.SaveChangesAsync();

        // Delete category containing active products unconditionally
        await admin.DeleteCategoryAsync(cat.Id, permanent: true);

        // Verify category is permanently deleted
        var checkCat = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cat.Id);
        await checkCat.Should().BeNull();

        // Verify product was safely reassigned to Uncategorized
        var checkProd = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == prod.Id);
        await checkProd.Should().NotBeNull();
        var uncategorized = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == "uncategorized");
        await uncategorized.Should().NotBeNull();
        await checkProd!.CategoryId.Should().BeEqualTo(uncategorized!.Id);
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

    [Test]
    public async Task SaveCategoryAsync_CategoryWithChildrenReparented_SuccessfullyPreservesTwoTierHierarchy()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var catA = new Category { Name = "Parent A " + Guid.NewGuid().ToString("N")[..6], Slug = "parent-a-" + Guid.NewGuid().ToString("N")[..6] };
        db.Categories.Add(catA);
        await db.SaveChangesAsync();

        var catB = new Category { Name = "Child B " + Guid.NewGuid().ToString("N")[..6], Slug = "child-b-" + Guid.NewGuid().ToString("N")[..6], ParentCategoryId = catA.Id };
        db.Categories.Add(catB);

        var catC = new Category { Name = "Top C " + Guid.NewGuid().ToString("N")[..6], Slug = "top-c-" + Guid.NewGuid().ToString("N")[..6] };
        db.Categories.Add(catC);
        await db.SaveChangesAsync();

        // Reparent catA under catC
        await admin.SaveCategoryAsync(new CategoryEditForm
        {
            Id = catA.Id,
            Name = catA.Name,
            Slug = catA.Slug,
            ParentId = catC.Id
        });

        var checkA = await db.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == catA.Id);
        await checkA.Should().NotBeNull();
        await checkA!.ParentCategoryId.Should().BeEqualTo(catC.Id);

        // Child B should be safely reparented to null so max 2-tier structure is maintained without error
        var checkB = await db.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == catB.Id);
        await checkB.Should().NotBeNull();
        await checkB!.ParentCategoryId.Should().BeNull();
    }

    [Test]
    public async Task UploadImageEndpoint_WithValidImage_ReturnsOkAndUrl()
    {
        using var content = new MultipartFormDataContent();
        var dummyBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        var fileContent = new ByteArrayContent(dummyBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test-image.png");

        var response = await AdminClient.PostAsync("/api/admin/uploads/image", content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Upload failed with HTTP {(int)response.StatusCode}: {body}");
        }

        await response.IsSuccessStatusCode.Should().BeTrue();
        await body.Should().Contain("url");
    }
}
