using System.Net.Http.Headers;
using System.Text.Json;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

[NotInParallel("web-integration")]
public class BrandLogoIntegrationTests
{
    private static readonly WebApplicationFactory<Program> Factory = new();

    [Test]
    public async Task BrandLogo_UploadEndpoint_ValidPng_ReturnsOkWithUrl()
    {
        var client = Factory.CreateClient();

        using var content = new MultipartFormDataContent();
        // 1x1 transparent PNG bytes
        byte[] pngBytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        ];
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
        var client = Factory.CreateClient();

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
        var client = Factory.CreateClient();

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
}
