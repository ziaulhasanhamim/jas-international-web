using System.Net.Http.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
namespace JASIntWeb.Tests.Integration;

public class AdminCatalogApiTests : JasIntegrationTestBase
{
    [Test]
    public async Task CatalogApi_RequiresAdministratorAndCurrentSecurityStamp()
    {
        using var customer = CustomerClient;
        await ((int)(await customer.GetAsync("/api/admin/categories/editor/0")).StatusCode).Should().BeEqualTo(403);
        using var admin = AdminClient;
        admin.DefaultRequestHeaders.Add("X-Test-SecurityStamp", "stale-stamp");
        await ((int)(await admin.GetAsync("/api/admin/categories/editor/0")).StatusCode).Should().BeEqualTo(401);
        using var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await ((int)(await anonymous.GetAsync("/api/admin/products/editor/0")).StatusCode).Should().BeEqualTo(302);
    }
    [Test]
    public async Task CatalogApi_CategoryAndBrandPersistEditorFieldsAndRejectMismatchedIds()
    {
        using var client = AdminClient;
        var category = new CategoryEditForm { Name = "API Category", Icon = "/uploads/api.png", OgImageUrl = "/jas-og-card.png", Featured = true, MetaTitle = "API title", MetaDescription = "API description", IntroText = "Intro", BottomSeoCopy = "Bottom" };
        var created = await client.PostAsJsonAsync("/api/admin/categories", category);
        await ((int)created.StatusCode).Should().BeEqualTo(201);
        var saved = (await created.Content.ReadFromJsonAsync<AdminSaveResult>())!;
        var loaded = (await client.GetFromJsonAsync<AdminEditorData<CategoryEditForm>>($"/api/admin/categories/editor/{saved.Id}"))!;
        await loaded.Input.Icon.Should().BeEqualTo(category.Icon);
        await loaded.Input.Featured.Should().BeTrue();
        await loaded.Input.MetaTitle.Should().BeEqualTo(category.MetaTitle);
        await loaded.Input.BottomSeoCopy.Should().BeEqualTo("Bottom");
        var categoryPage=await client.GetStringAsync($"/category/{loaded.Input.Slug}");
        await categoryPage.Contains("<title>API title</title>").Should().BeTrue();
        await categoryPage.Contains("content=\"API description\"").Should().BeTrue();
        await System.Text.RegularExpressions.Regex.Match(categoryPage, "<meta property=\"og:image\" content=\"([^\"]+)\"").Groups[1].Value.EndsWith("/jas-og-card.png").Should().BeTrue();
        loaded.Input.Hidden = true;
        await ((int)(await client.PutAsJsonAsync($"/api/admin/categories/{saved.Id + 1}", loaded.Input)).StatusCode).Should().BeEqualTo(400);
        await (await client.GetFromJsonAsync<AdminEditorData<CategoryEditForm>>($"/api/admin/categories/editor/{saved.Id}"))!.Input.Hidden.Should().BeFalse();
        await ((int)(await client.PutAsJsonAsync($"/api/admin/categories/{saved.Id}", loaded.Input)).StatusCode).Should().BeEqualTo(200);
        await (await client.GetFromJsonAsync<AdminEditorData<CategoryEditForm>>($"/api/admin/categories/editor/{saved.Id}"))!.Input.Hidden.Should().BeTrue();
        var brand = new BrandEditForm { Name = "API Brand", LogoUrl = "/jas-logo.png", MetaTitle = "Brand title", Description = "Brand description", IsFeatured = true };
        var result = await client.PostAsJsonAsync("/api/admin/brands", brand);
        await ((int)result.StatusCode).Should().BeEqualTo(201);
        var brandId = (await result.Content.ReadFromJsonAsync<AdminSaveResult>())!.Id;
        var brandData = (await client.GetFromJsonAsync<AdminEditorData<BrandEditForm>>($"/api/admin/brands/editor/{brandId}"))!;
        await brandData.Input.LogoUrl.Should().BeEqualTo(brand.LogoUrl);
        await brandData.Input.IsFeatured.Should().BeTrue();
        brandData.Input.LogoUrl = "/jas-og-card.png";
        await ((int)(await client.PutAsJsonAsync($"/api/admin/brands/{brandId}", brandData.Input)).StatusCode).Should().BeEqualTo(200);
        await (await client.GetFromJsonAsync<AdminEditorData<BrandEditForm>>($"/api/admin/brands/editor/{brandId}"))!.Input.LogoUrl.Should().BeEqualTo("/jas-og-card.png");
        await using (var scope = CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Where(p => p.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(p => p.BrandId, brandId));
        await ((int)(await client.DeleteAsync($"/api/admin/brands/{brandId}")).StatusCode).Should().BeEqualTo(409);
        await using (var scope = CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Where(p => p.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(p => p.BrandId, (int?)null));
        await ((int)(await client.DeleteAsync($"/api/admin/brands/{brandId}")).StatusCode).Should().BeEqualTo(200);
    }
    [Test]
    public async Task CatalogApi_ValidatesInputsAndMissingRecordsWithoutWriting()
    {
        using var client = AdminClient;
        var invalid = await client.PostAsJsonAsync("/api/admin/categories", new CategoryEditForm());
        await ((int)invalid.StatusCode).Should().BeEqualTo(400);
        var errors = (await invalid.Content.ReadFromJsonAsync<AdminCatalogError>())!;
        await errors.Errors!.ContainsKey("Name").Should().BeTrue();
        await ((int)(await client.GetAsync("/api/admin/brands/editor/999999")).StatusCode).Should().BeEqualTo(404);
    }
    [Test]
    public async Task CatalogApi_ProductPersistsMediaRichTextFlagsAndSimpleInventory()
    {
        using var client = AdminClient;
        var form = new ProductEditForm { Name = "API Product", CategoryId = 1, Description = "A useful product", Details = "<p>Rich details &amp; warranty</p>", Image = "/jas-logo.png", Images = "/jas-og-card.png", BasePrice = 500, CostPrice = 200, InitialStock = 4, IsFeatured = true, IsNewArrival = false, MetaTitle = "API Product title", OgImageUrl = "/jas-og-card.png", WarrantyInfo = "Two years" };
        var response = await client.PostAsJsonAsync("/api/admin/products", form);
        await ((int)response.StatusCode).Should().BeEqualTo(201);
        var result = (await response.Content.ReadFromJsonAsync<AdminSaveResult>())!;
        var loaded = (await client.GetFromJsonAsync<AdminEditorData<ProductEditForm>>($"/api/admin/products/editor/{result.Id}"))!;
        await loaded.Input.Image.Should().BeEqualTo(form.Image);
        await loaded.Input.Images.Should().BeEqualTo(form.Images);
        await loaded.Input.Details.Should().BeEqualTo(form.Details);
        await loaded.Input.IsFeatured.Should().BeTrue();
        await loaded.Input.IsNewArrival.Should().BeFalse();
        await loaded.Input.InitialStock.Should().BeEqualTo(4);
        await loaded.Input.WarrantyInfo.Should().BeEqualTo("Two years");
        var productPage=await client.GetStringAsync($"/product/{loaded.Input.Slug}");
        await productPage.Contains("<title>API Product title</title>").Should().BeTrue();
        await System.Text.RegularExpressions.Regex.Match(productPage, "<meta property=\"og:image\" content=\"([^\"]+)\"").Groups[1].Value.EndsWith("/jas-og-card.png").Should().BeTrue();
        loaded.Input.Details = "<p>Updated rich text</p>";
        loaded.Input.Image = "/jas-og-card.png";
                loaded.Input.Hidden = true;
        await ((int)(await client.PutAsJsonAsync($"/api/admin/products/{result.Id}", loaded.Input)).StatusCode).Should().BeEqualTo(200);
        var updated = (await client.GetFromJsonAsync<AdminEditorData<ProductEditForm>>($"/api/admin/products/editor/{result.Id}"))!;
        await updated.Input.Details.Should().BeEqualTo("<p>Updated rich text</p>");
        await updated.Input.Image.Should().BeEqualTo("/jas-og-card.png");
        await updated.Input.Hidden.Should().BeTrue();
    }
    [Test]
    public async Task CatalogApi_RechecksDatabaseRoleForAnAlreadyAuthenticatedAdministrator()
    {
        using var client = AdminClient;
        await ((int)(await client.GetAsync("/api/admin/categories/editor/0")).StatusCode).Should().BeEqualTo(200);
        await using (var scope = CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserRoles.Where(role => role.UserId == 1).ExecuteDeleteAsync();
        await ((int)(await client.GetAsync("/api/admin/categories/editor/0")).StatusCode).Should().BeEqualTo(401);
    }

    private sealed class StrictFactory : JasWebApplicationFactory { protected override bool UseRealAntiforgery => true; }
    private static readonly StrictFactory Strict = new();
    [Test]
    public async Task CatalogApi_RealAntiforgeryRejectsMissingAndWrongTokenAndAcceptsCorrectToken()
    {
        using var client = Strict.CreateAdminClient();
        var input = new CategoryEditForm { Name = "Protected API category" };
        await ((int)(await client.PostAsJsonAsync("/api/admin/categories", input)).StatusCode).Should().BeEqualTo(400);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "wrong-token");
        await ((int)(await client.PostAsJsonAsync("/api/admin/categories", input)).StatusCode).Should().BeEqualTo(400);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var token = await client.GetFromJsonAsync<AntiforgeryDto>("/api/admin/uploads/security");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
        var valid = await client.PostAsJsonAsync("/api/admin/categories", input);
        await ((int)valid.StatusCode).Should().BeEqualTo(201);
        var id = (await valid.Content.ReadFromJsonAsync<AdminSaveResult>())!.Id;
        var read = await client.GetAsync($"/api/admin/categories/editor/{id}");
        await read.Headers.CacheControl!.NoStore.Should().BeTrue();
    }
    [Test]
    public async Task CategoryList_RendersImageAsThumbnailAndSortsGloballyBeforePaging()
    {
        await using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 55; i++) db.Categories.Add(new Category { Name = $"Sort {i:00}", Slug = $"sort-{i}", Icon = "https://example.test/icon.png" });
            await db.SaveChangesAsync();
        }
        using var client = AdminClient;
        var html = await client.GetStringAsync("/admin/categories?sort=Name&direction=desc&page=1");
        await html.Contains("src=\"https://example.test/icon.png\"").Should().BeTrue();
        await html.Contains("Sort 54").Should().BeTrue();
        await html.Contains("Sort 00</strong>").Should().BeFalse();
        await html.Contains("direction=asc").Should().BeTrue();
    }
}
