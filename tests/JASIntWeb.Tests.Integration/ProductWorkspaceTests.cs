using System.Net.Http.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace JASIntWeb.Tests.Integration;

public class ProductWorkspaceTests : JasIntegrationTestBase
{
    [Test]
    public async Task CatalogContent_PreviewMatchesPublicBrandAndCategoryOutput()
    {
        using var client = AdminClient; const string source = "## Genuine audio\n\n**Warranty** and support.\n\n<script>alert(1)</script>";
        var response = await client.PostAsJsonAsync("/api/admin/content/preview", new ContentPreviewRequest(source)); await ((int)response.StatusCode).Should().BeEqualTo(200);
        var preview = (await response.Content.ReadFromJsonAsync<ContentPreview>())!.Html;
        await using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Categories.Where(c => c.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(c => c.IntroText, "Intro above grid").SetProperty(c => c.BottomSeoCopy, source));
            await db.Brands.Where(b => b.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(b => b.BottomSeoCopy, source));
            await scope.ServiceProvider.GetRequiredService<CatalogService>().InvalidateAllAsync();
        }
        await using var read = CreateScope(); var dbRead = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = await dbRead.Categories.SingleAsync(c => c.Id == 1); var brand = await dbRead.Brands.SingleAsync(b => b.Id == 1);
        var categoryPage = await client.GetStringAsync($"/category/{category.Slug}"); var brandPage = await client.GetStringAsync($"/brand/{brand.Slug}");
        await categoryPage.Contains(preview).Should().BeTrue(); await brandPage.Contains(preview).Should().BeTrue(); await categoryPage.Contains("Intro above grid").Should().BeTrue(); await preview.Contains("<script").Should().BeFalse();
    }
}
