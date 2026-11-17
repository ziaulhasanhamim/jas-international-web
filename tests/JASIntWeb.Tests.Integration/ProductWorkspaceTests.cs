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
    private static string Path(int id = 1, string suffix = "") => $"/api/admin/products/{id}/workspace{suffix}";
    private async Task<AttributeDefinition> Definition()
    {
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var definition = new AttributeDefinition { Name = "Color", Code = "color", Options = [new() { Value = "Black", Code = "black" }] };
        db.AttributeDefinitions.Add(definition); await db.SaveChangesAsync(); return definition;
    }
    private static async Task<ProductWorkspace> Read(HttpClient client, int id = 1) => (await client.GetFromJsonAsync<ProductWorkspace>(Path(id)))!;
    private static ProductConfiguration Configuration(ProductWorkspace workspace, AttributeDefinition definition, string value = "Matte Black")
    {
        workspace.Attributes.Clear(); workspace.Attributes.Add(new() { DefinitionId = definition.Id, Name = definition.Name, Options = [new() { Id = -1, Value = value, SuggestionId = definition.Options.First().Id }] });
        workspace.Variants[0].Choices = new() { [definition.Id] = -1 };
        return new(workspace.Revision, workspace.Attributes, workspace.Variants);
    }
    [Test]
    public async Task Workspace_ProductValuesAreIndependentSuggestionsAndPreserveVariantIdentity()
    {
        var definition = await Definition(); using var client = AdminClient;
        var first = await Read(client); var original = first.Variants[0];
        var response = await client.PutAsJsonAsync(Path(suffix: "/configuration"), Configuration(first, definition)); await ((int)response.StatusCode).Should().BeEqualTo(200);
        first = (await response.Content.ReadFromJsonAsync<ProductWorkspace>())!;
        var second = await Read(client, 2); await ((int)(await client.PutAsJsonAsync(Path(2, "/configuration"), Configuration(second, definition, "Midnight Black"))).StatusCode).Should().BeEqualTo(200);
        first.Attributes[0].Options[0].Value = "Matte Charcoal";
        var code = first.Attributes[0].Options[0].Code;
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), new ProductConfiguration(first.Revision, first.Attributes, first.Variants))).StatusCode).Should().BeEqualTo(200);
        var saved = await Read(client); await saved.Variants[0].Id.Should().BeEqualTo(original.Id); await saved.Variants[0].OnHand.Should().BeEqualTo(original.OnHand);
        await saved.Attributes[0].Options[0].Code.Should().BeEqualTo(code);
        await (await Read(client, 2)).Attributes[0].Options[0].Value.Should().BeEqualTo("Midnight Black");
        await using var scope = CreateScope(); await (await scope.ServiceProvider.GetRequiredService<AppDbContext>().AttributeOptions.SingleAsync()).Value.Should().BeEqualTo("Black");
        await scope.ServiceProvider.GetRequiredService<AdminService>().SaveOptionAsync(new() { Id = definition.Options.Single().Id, AttributeDefinitionId = definition.Id, Value = "Universal black", Code = "black" });
        await (await Read(client)).Attributes[0].Options[0].Value.Should().BeEqualTo("Matte Charcoal");
        await (await Read(client, 2)).Attributes[0].Options[0].Value.Should().BeEqualTo("Midnight Black");
        var slug = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Where(p => p.Id == 1).Select(p => p.Slug).SingleAsync();
        var page = await client.GetStringAsync($"/product/{slug}");
        await page.Contains("Matte Charcoal").Should().BeTrue();

    }
    [Test]
    public async Task Workspace_RejectsDuplicatesForeignOptionsAndStaleConfigurationWithoutPartialWrites()
    {
        var definition = await Definition(); using var client = AdminClient;
        var workspace = await Read(client); var command = Configuration(workspace, definition);
        workspace.Attributes[0].Options.Add(new() { Id = -2, Value = " matte black " });
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), command)).StatusCode).Should().BeEqualTo(400);
        await (await Read(client)).Attributes.Count.Should().BeEqualTo(0);
        workspace.Attributes[0].Options.RemoveAt(1);
        var savedResponse = await client.PutAsJsonAsync(Path(suffix: "/configuration"), command); await ((int)savedResponse.StatusCode).Should().BeEqualTo(200);
        var saved = (await savedResponse.Content.ReadFromJsonAsync<ProductWorkspace>())!;
        var other = await Read(client, 2); var foreign = Configuration(other, definition); other.Attributes[0].Options[0].Id = saved.Attributes[0].Options[0].Id; other.Variants[0].Choices[definition.Id] = saved.Attributes[0].Options[0].Id;
        await ((int)(await client.PutAsJsonAsync(Path(2, "/configuration"), foreign)).StatusCode).Should().BeEqualTo(400);
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), command)).StatusCode).Should().BeEqualTo(409);
        await (await Read(client, 2)).Attributes.Count.Should().BeEqualTo(0);
    }
    [Test]
    public async Task Workspace_GenerationPreviewWritesNothingAndReplayCreatesNoDuplicates()
    {
        var definition = await Definition(); using var client = AdminClient;
        var workspace = await Read(client); var command = Configuration(workspace, definition);
        workspace.Attributes[0].Options.Add(new() { Id = -2, Value = "Midnight Black" });
        var response = await client.PutAsJsonAsync(Path(suffix: "/configuration"), command); await ((int)response.StatusCode).Should().BeEqualTo(200);
        workspace = await Read(client); var before = workspace.Variants.Count;
        var previewResponse = await client.PostAsJsonAsync(Path(suffix: "/preview"), new WorkspaceRevision(workspace.Revision)); var preview = (await previewResponse.Content.ReadFromJsonAsync<CombinationPreview>())!;
        await preview.Total.Should().BeEqualTo(2); await preview.Missing.Should().BeEqualTo(1); await (await Read(client)).Variants.Count.Should().BeEqualTo(before);
        var generated = await client.PostAsJsonAsync(Path(suffix: "/generate"), new WorkspaceRevision(workspace.Revision)); await ((int)generated.StatusCode).Should().BeEqualTo(200);
        await ((int)(await client.PostAsJsonAsync(Path(suffix: "/generate"), new WorkspaceRevision(workspace.Revision))).StatusCode).Should().BeEqualTo(409);
        workspace = await Read(client); await workspace.Variants.Count.Should().BeEqualTo(2); var added = workspace.Variants.Single(v => v.Id != 1);
        await added.OnHand.Should().BeEqualTo(0); await added.Active.Should().BeFalse(); await added.SellingPrice.Should().BeEqualTo(0);
        await ((int)(await client.PostAsJsonAsync(Path(suffix: "/generate"), new WorkspaceRevision(workspace.Revision))).StatusCode).Should().BeEqualTo(200);
        await (await Read(client)).Variants.Count.Should().BeEqualTo(2);
    }
    [Test]
    public async Task Workspace_ConcurrentVariantEditsHaveOneWinnerAndDetailsCannotChangePriceOrStock()
    {
        using var client = AdminClient; var workspace = await Read(client); var variant = workspace.Variants[0]; variant.SellingPrice = 15000; variant.CompareAtPrice = 16000;
        var responses = await Task.WhenAll(client.PutAsJsonAsync(Path(suffix: "/variant"), variant), client.PutAsJsonAsync(Path(suffix: "/variant"), variant));
        await responses.Count(r => r.IsSuccessStatusCode).Should().BeEqualTo(1); await responses.Count(r => (int)r.StatusCode == 409).Should().BeEqualTo(1);
        var details = (await client.GetFromJsonAsync<AdminEditorData<ProductEditForm>>("/api/admin/products/editor/1"))!;
        details.Input.BasePrice = 1; details.Input.CostPrice = 1; details.Input.InitialStock = 1; details.Input.Sku = "UNWANTED"; details.Input.CompareAtPrice = 1;
        await ((int)(await client.PutAsJsonAsync("/api/admin/products/1", details.Input)).StatusCode).Should().BeEqualTo(200);
        var saved = (await Read(client)).Variants[0]; await saved.SellingPrice.Should().BeEqualTo(15000); await saved.CompareAtPrice.Should().BeEqualTo(16000); await saved.OnHand.Should().BeEqualTo(variant.OnHand); await saved.Sku.Should().BeEqualTo(variant.Sku);

    }
    [Test]
    public async Task Workspace_StockAdjustmentsAreExplicitReservedSafeAndLedgerBacked()
    {
        using var client = AdminClient; var workspace = await Read(client); var variant = workspace.Variants[0];
        await using (var scope = CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryItems.Where(i => i.ProductVariantId == variant.Id).ExecuteUpdateAsync(update => update.SetProperty(i => i.QuantityReserved, 3));
        var invalid = new WorkspaceStock(workspace.Revision, variant.Id, variant.OnHand, 2, "Count correction");
        await ((int)(await client.PostAsJsonAsync(Path(suffix: "/stock"), invalid)).StatusCode).Should().BeEqualTo(409);
        var valid = invalid with { Quantity = variant.OnHand + 2 };
        await ((int)(await client.PostAsJsonAsync(Path(suffix: "/stock"), valid)).StatusCode).Should().BeEqualTo(200);
        await ((int)(await client.PostAsJsonAsync(Path(suffix: "/stock"), valid)).StatusCode).Should().BeEqualTo(409);
        var saved = (await Read(client)).Variants[0]; await saved.OnHand.Should().BeEqualTo(variant.OnHand + 2); await saved.Reserved.Should().BeEqualTo(3);
        await using var check = CreateScope(); var transaction = await check.ServiceProvider.GetRequiredService<AppDbContext>().InventoryTransactions.SingleAsync(t => t.Note == "Count correction"); await transaction.QuantityChange.Should().BeEqualTo(2);
    }
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
    [Test]
    public async Task Workspace_RejectsInvalidPricesSkuReuseAndDefaultArchival()
    {
        using var client = AdminClient; var workspace = await Read(client); var variant = workspace.Variants[0];
        variant.CompareAtPrice = variant.SellingPrice;
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/variant"), variant)).StatusCode).Should().BeEqualTo(400);
        variant.CompareAtPrice = null; variant.SellingPrice = 0;
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/variant"), variant)).StatusCode).Should().BeEqualTo(400);
        variant.SellingPrice = 100; variant.Sku = (await Read(client, 2)).Variants[0].Sku;
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/variant"), variant)).StatusCode).Should().BeEqualTo(409);
        variant.Sku = workspace.Name; variant.Default = false; variant.Archived = true; variant.Active = false;
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/variant"), variant)).StatusCode).Should().BeEqualTo(400);
        await (await Read(client)).Variants[0].Default.Should().BeTrue();
    }
    [Test]
    public async Task Workspace_RequiresMappingsAndPreservesReferencedValuesOnFailure()
    {
        var definition = await Definition(); using var client = AdminClient; var workspace = await Read(client);
        var command = Configuration(workspace, definition); workspace.Variants[0].Choices.Clear();
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), command)).StatusCode).Should().BeEqualTo(400);
        workspace.Variants[0].Choices[definition.Id] = -1;
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), command)).StatusCode).Should().BeEqualTo(200);
        workspace = await Read(client); var originalId = workspace.Attributes[0].Options[0].Id; workspace.Attributes[0].Options.Clear();
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), new ProductConfiguration(workspace.Revision, workspace.Attributes, workspace.Variants))).StatusCode).Should().BeEqualTo(400);
        await (await Read(client)).Attributes[0].Options[0].Id.Should().BeEqualTo(originalId);
    }
    [Test]
    public async Task Workspace_GenerationAcceptsOneHundredAndRejectsOneHundredOne()
    {
        var definition = await Definition(); using var client = AdminClient; var workspace = await Read(client); var command = Configuration(workspace, definition);
        workspace.Attributes[0].Required = false;
        for (var i = 2; i <= 100; i++) workspace.Attributes[0].Options.Add(new() { Id = -i, Value = "Value " + i });
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), command)).StatusCode).Should().BeEqualTo(200);
        workspace = await Read(client);
        await ((int)(await client.PostAsJsonAsync(Path(suffix: "/preview"), new WorkspaceRevision(workspace.Revision))).StatusCode).Should().BeEqualTo(400);
        workspace.Attributes[0].Required = true;
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), new ProductConfiguration(workspace.Revision, workspace.Attributes, workspace.Variants))).StatusCode).Should().BeEqualTo(200);
        workspace = await Read(client); var response = await client.PostAsJsonAsync(Path(suffix: "/preview"), new WorkspaceRevision(workspace.Revision));
        await ((int)response.StatusCode).Should().BeEqualTo(200); await (await response.Content.ReadFromJsonAsync<CombinationPreview>())!.Total.Should().BeEqualTo(100);
        await workspace.Variants.Count.Should().BeEqualTo(1);
    }
    private sealed class StrictFactory : JasWebApplicationFactory { protected override bool UseRealAntiforgery => true; }
    [Test]
    public async Task Workspace_RequiresAdministratorAndRealAntiforgeryForMutationsAndPreview()
    {
        using var customer = CustomerClient;
        await ((int)(await customer.GetAsync(Path())).StatusCode).Should().BeEqualTo(403);
        using var factory = new StrictFactory(); using var client = factory.CreateAdminClient();
        var workspace = await Read(client);
        await ((int)(await client.PostAsJsonAsync(Path(suffix: "/preview"), new WorkspaceRevision(workspace.Revision))).StatusCode).Should().BeEqualTo(400);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid");
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/variant"), workspace.Variants[0])).StatusCode).Should().BeEqualTo(400);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var security = (await client.GetFromJsonAsync<AntiforgeryDto>("/api/admin/uploads/security"))!;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", security.Token);
        var valid = await client.PostAsJsonAsync(Path(suffix: "/preview"), new WorkspaceRevision(workspace.Revision));
        await ((int)valid.StatusCode).Should().BeEqualTo(200); await valid.Headers.CacheControl!.NoStore.Should().BeTrue();
        client.DefaultRequestHeaders.Add("X-Test-SecurityStamp", "expired");
        await ((int)(await client.GetAsync(Path())).StatusCode).Should().BeEqualTo(401);
    }
    [Test]
    public async Task Workspace_VariantPriceFlowsToCatalogDetailsCartAndFeeds()
    {
        using var client = AdminClient; var workspace = await Read(client); var variant = workspace.Variants[0]; variant.SellingPrice = 12300; variant.CompareAtPrice = 15000;
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/variant"), variant)).StatusCode).Should().BeEqualTo(200);
        await using var scope = CreateScope(); var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();
        var slug = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Where(p => p.Id == 1).Select(p => p.Slug).SingleAsync();
        var details = (await catalog.DetailsAsync(slug))!; await details.Variants.Single(v => v.Id == variant.Id).OriginalPrice.Should().BeEqualTo(15000);
        var latest = await catalog.LatestAsync(null, 20); var summary = latest.Single(p => p.Id == 1); await summary.Price.Should().BeEqualTo(12300); await summary.OriginalPrice.Should().BeEqualTo(15000);
        var page = await client.GetStringAsync($"/product/{slug}"); await page.Contains("15,000").Should().BeTrue(); await page.Contains("12,300").Should().BeTrue();
        var cart = await scope.ServiceProvider.GetRequiredService<ShoppingService>().ResolveAsync([new(variant.Id, 2)]);
        await cart.Items.Single().Price.Should().BeEqualTo(12300);
        // Feed endpoints retain their configured token gate; inspect the authorized underlying item via the rendered feed when no gate is configured in tests.
        var feed = await client.GetAsync("/feeds/google.xml");
        await feed.IsSuccessStatusCode.Should().BeTrue(); var xml = await feed.Content.ReadAsStringAsync(); await xml.Contains("12300.00 BDT").Should().BeTrue(); await xml.Contains("15000.00 BDT").Should().BeTrue();
    }
    [Test]
    public async Task Workspace_UsedAttributeCodesCannotChangeSelectionUrls()
    {
        var definition = await Definition(); using var client = AdminClient; var workspace = await Read(client);
        await ((int)(await client.PutAsJsonAsync(Path(suffix: "/configuration"), Configuration(workspace, definition))).StatusCode).Should().BeEqualTo(200);
        await using var scope = CreateScope(); var service = scope.ServiceProvider.GetRequiredService<AdminService>();
        var rejected = false;
        try { await service.SaveAttributeAsync(new() { Id = definition.Id, Name = "Colour", Code = "changed-color" }); }
        catch (ShopException) { rejected = true; }
        await rejected.Should().BeTrue();
        await (await scope.ServiceProvider.GetRequiredService<AppDbContext>().AttributeDefinitions.AsNoTracking().SingleAsync()).Code.Should().BeEqualTo("color");
    }
}
