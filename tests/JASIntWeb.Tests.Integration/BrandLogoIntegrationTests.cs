using System.Net.Http.Headers;
using System.Text.Json;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class BrandLogoIntegrationTests : JasIntegrationTestBase
{

    [Test]
    public async Task BrandLogo_UploadEndpoint_ValidPng_ReturnsOkWithUrl()
    {
        var client = AdminClient;

        using var content = new MultipartFormDataContent();
        using var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.White, 2, 2);
        var pngBytes = image.ToByteArray(ImageMagick.MagickFormat.Png);
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test-brand-logo.png");

        var response = await client.PostAsync("/api/admin/uploads/brand-logo", content);
        await ((int)response.StatusCode).Should().BeEqualTo(200);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var url = doc.RootElement.GetProperty("url").GetString();
        await url.Should().NotBeNull();
        await url!.Should().StartWith("/uploads/");
    }

    [Test]
    public async Task BrandLogo_UploadEndpoint_InvalidExtension_ReturnsBadRequest()
    {
        var client = AdminClient;

        using var content = new MultipartFormDataContent();
        var fileContent = new StringContent("invalid executable content");
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "malicious.exe");

        var response = await client.PostAsync("/api/admin/uploads/brand-logo", content);
        await ((int)response.StatusCode).Should().BeEqualTo(400);
    }

    [Test]
    public async Task BrandLogo_UploadEndpoint_EmptyFile_ReturnsBadRequest()
    {
        var client = AdminClient;

        using var content = new MultipartFormDataContent();
        var emptyContent = new ByteArrayContent([]);
        emptyContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(emptyContent, "file", "empty.png");

        var response = await client.PostAsync("/api/admin/uploads/brand-logo", content);
        await ((int)response.StatusCode).Should().BeEqualTo(400);
    }

    [Test]
    public async Task BrandEditor_Page_UnauthenticatedRedirectsToLogin()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/admin/brands/new");
        var body = await response.Content.ReadAsStringAsync();

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await body.Should().Contain("Sign In");
        await body.Should().Contain("/account/login?ReturnUrl=%2Fadmin%2Fbrands%2Fnew");
    }

    [Test]
    public async Task SaveBrandAsync_WithLogoUrl_PersistsLogoUrl()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var slug = "test-logo-brand-" + Guid.NewGuid().ToString("N")[..6];
        var form = new BrandEditForm
        {
            Name = "Logo Test Brand",
            Slug = slug,
            LogoUrl = "/uploads/test-logo.png",
            Description = "Brand for testing logo upload",
            SortOrder = 50,
            IsFeatured = true
        };

        await admin.SaveBrandAsync(form);

        var saved = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.Slug == slug);
        await saved.Should().NotBeNull();
        await saved!.LogoUrl.Should().BeEqualTo("/uploads/test-logo.png");
        await saved.Name.Should().BeEqualTo("Logo Test Brand");
    }

    [Test]
    public async Task SaveBrandAsync_ExistingBrand_UpdatesInPlaceWithoutCreatingNewRecord()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var slug = "brand-update-" + Guid.NewGuid().ToString("N")[..6];
        var initialForm = new BrandEditForm
        {
            Name = "Initial Brand Name",
            Slug = slug,
            Description = "Initial description",
            SortOrder = 10
        };

        await admin.SaveBrandAsync(initialForm);

        var existing = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.Slug == slug);
        await existing.Should().NotBeNull();
        var existingId = existing!.Id;

        var countBefore = await db.Brands.CountAsync();

        // Update the brand with new name and description
        var updateForm = new BrandEditForm
        {
            Id = existingId,
            Name = "Updated Brand Name",
            Slug = slug,
            Description = "Updated brand description",
            SortOrder = 20
        };

        await admin.SaveBrandAsync(updateForm);

        var countAfter = await db.Brands.CountAsync();
        // Brand count MUST not increase on update!
        await countAfter.Should().BeEqualTo(countBefore);

        var updated = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.Id == existingId);
        await updated.Should().NotBeNull();
        await updated!.Name.Should().BeEqualTo("Updated Brand Name");
        await updated.Description.Should().BeEqualTo("Updated brand description");
        await updated.SortOrder.Should().BeEqualTo(20);
    }

    [Test]
    public async Task DeleteBrandAsync_WithActiveProducts_ThrowsShopException()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var slug = "brand-del-" + Guid.NewGuid().ToString("N")[..6];
        var brandForm = new BrandEditForm
        {
            Name = "Brand To Delete",
            Slug = slug
        };
        await admin.SaveBrandAsync(brandForm);
        var brand = await db.Brands.FirstAsync(b => b.Slug == slug);

        // Add an active product linked to this brand
        var category = await db.Categories.FirstAsync();
        var product = new JASIntWeb.Persistence.Entities.Product
        {
            Name = "Product for brand delete test",
            Slug = "prod-brand-del-" + Guid.NewGuid().ToString("N")[..6],
            Details = "Details",
            MainImageUrl = "/test.jpg",
            CategoryId = category.Id,
            BrandId = brand.Id
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Attempting to delete brand with active products should throw ShopException!
        await Assert.ThrowsAsync<ShopException>(() => admin.DeleteBrandAsync(brand.Id, permanent: true));
    }
}
