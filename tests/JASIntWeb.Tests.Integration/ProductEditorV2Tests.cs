using System.Net.Http.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace JASIntWeb.Tests.Integration;

public class ProductEditorV2Tests : JasIntegrationTestBase
{
    private const string Root = "/api/admin/product-editor";
    private static async Task<ProductEditorDraft> Read(HttpClient c, int id = 1) => (await c.GetFromJsonAsync<ProductEditorData>($"{Root}/{id}"))!.Draft;
    private static Task<HttpResponseMessage> Send(HttpClient c, ProductEditorDraft d, Guid? operation = null) => d.Id == 0 ? c.PostAsJsonAsync(Root, new EditorSave(operation ?? Guid.NewGuid(), d)) : c.PutAsJsonAsync($"{Root}/{d.Id}", new EditorSave(operation ?? Guid.NewGuid(), d));
    private static async Task<ProductEditorDraft> Save(HttpClient c, ProductEditorDraft d, Guid? operation = null)
    {
        var response = await Send(c, d, operation);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Save: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<EditorCommit>())!.Editor!.Draft;
    }
    private async Task<EditorAttribute> Axis(string name = "Color", int count = 2)
    {
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = new AttributeDefinition { Name = name, Code = name.ToLowerInvariant(), Options = [new() { Value = "Black", Code = "black" }] };
        db.AttributeDefinitions.Add(row); await db.SaveChangesAsync();
        return new() { DefinitionId = row.Id, Name = name, Options = Enumerable.Range(0, count).Select(i => new EditorOption { Value = $"{name} {i}", SuggestionId = i == 0 ? row.Options.First().Id : null }).ToList() };
    }
    private static void Map(ProductEditorDraft d, params EditorAttribute[] axes)
    { d.Attributes = [.. axes]; d.Variants[0].Choices = axes.ToDictionary(a => a.DefinitionId, a => a.Options[0].ClientId); }

    [Test]
    public async Task DraftNeedsOnlyNameCategoryAndCreatesNoPlaceholderVariant()
    {
        using var c = AdminClient; var draft = await Read(c, 0); draft.Information.Name = "Name only draft"; draft.Information.CategoryId = 1;
        var saved = await Save(c, draft); await saved.Variants.Count.Should().BeEqualTo(0); await saved.Information.Hidden.Should().BeTrue();
        saved.Information.Hidden = false;
        await ((int)(await Send(c, saved)).StatusCode).Should().BeEqualTo(422);
        await (await Read(c, saved.Id)).Information.Hidden.Should().BeTrue();
    }
    [Test]
    public async Task AtomicPublishWithOpeningStockIsReplaySafeAndRecoveryReturnsReceipt()
    {
        using var c = AdminClient; var d = await Read(c, 0); d.Information = new() { Name = "Atomic publish", CategoryId = 1, Hidden = false, Image = "/jas-logo.png", Details = "<p>Rich specs.</p>" };
        d.Variants = [new() { Active = true, Default = true, Price = 123.45m, OpeningStock = 7 }];
        var operation = Guid.NewGuid(); var results = await Task.WhenAll(Send(c, d, operation), Send(c, d, operation));
        await results.All(r => r.IsSuccessStatusCode).Should().BeTrue();
        var commits = await Task.WhenAll(results.Select(r => r.Content.ReadFromJsonAsync<EditorCommit>()));
        await commits[0]!.Receipt.ProductId.Should().BeEqualTo(commits[1]!.Receipt.ProductId);
        var recovered = (await c.GetFromJsonAsync<EditorCommit>($"{Root}/operations/{operation}"))!;
        await recovered.Editor!.Draft.Variants.Single().OnHand.Should().BeEqualTo(7);
        await recovered.Editor.Draft.Variants.Single().Sku.Contains("-BASE-").Should().BeTrue();
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await (await db.InventoryTransactions.CountAsync(t => t.ReferenceType == "ProductOpeningStock")).Should().BeEqualTo(1);
        await (await db.Products.CountAsync(p => p.Name == "Atomic publish")).Should().BeEqualTo(1);
        d.Information.Name = "Different payload"; await ((int)(await Send(c, d, operation)).StatusCode).Should().BeEqualTo(409);
    }
    [Test]
    public async Task FailedVariantValidationRollsBackProductAndOpeningStock()
    {
        using var c = AdminClient; var d = await Read(c, 0); d.Information.Name = "Must roll back"; d.Information.CategoryId = 1;
        d.Variants = [new() { Price = -1, OpeningStock = 10 }];
        await ((int)(await Send(c, d)).StatusCode).Should().BeEqualTo(422);
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await (await db.Products.AnyAsync(p => p.Name == "Must roll back")).Should().BeFalse();
        await (await db.ProductEditorReceipts.CountAsync()).Should().BeEqualTo(0);
        await (await db.InventoryTransactions.CountAsync(t => t.ReferenceType == "ProductOpeningStock")).Should().BeEqualTo(0);
    }
    [Test]
    public async Task SparsePickerPagesHugeUnsavedMatrixWithoutWritingAndAddsOnlySelected()
    {
        using var c = AdminClient; var d = await Read(c); var a = await Axis(count: 100); var b = await Axis("Storage", 100); Map(d, a, b);
        var response = await c.PostAsJsonAsync($"{Root}/preview", new EditorCombinationQuery(d));
        var page = (await response.Content.ReadFromJsonAsync<EditorCombinationPage>())!;
        await page.Total.Should().BeEqualTo("10000"); await page.Rows.Count.Should().BeEqualTo(50);
        await (await Read(c)).Attributes.Count.Should().BeEqualTo(0);
        foreach (var row in page.Rows.Skip(1).Take(2)) d.Variants.Add(new() { Choices = row.Choices, Price = 123, OpeningStock = 0 });
        var saved = await Save(c, d); await saved.Variants.Count.Should().BeEqualTo(3);
        var later = (await (await c.PostAsJsonAsync($"{Root}/preview", new EditorCombinationQuery(saved, "9950"))).Content.ReadFromJsonAsync<EditorCombinationPage>())!;
        await later.Rows.Count.Should().BeEqualTo(50); await later.HasMore.Should().BeFalse();
        var filtered = (await (await c.PostAsJsonAsync($"{Root}/preview", new EditorCombinationQuery(saved, Filters: new() { [b.DefinitionId] = [saved.Attributes[1].Options[0].ClientId] }))).Content.ReadFromJsonAsync<EditorCombinationPage>())!;
        await filtered.Total.Should().BeEqualTo("100");
    }
    [Test]
    public async Task VariantCapIncludesInactiveRowsAndDuplicateCombinationsAreRejected()
    {
        using var c = AdminClient; var d = await Read(c); var a = await Axis(count: 100); a.Required = false; Map(d, a);
        d.Variants.Add(new() { Choices = new(d.Variants[0].Choices) });
        await ((int)(await Send(c, d)).StatusCode).Should().BeEqualTo(409); d.Variants.RemoveAt(1);
        foreach (var value in a.Options.Skip(1)) d.Variants.Add(new() { Choices = new() { [a.DefinitionId] = value.ClientId } });
        var saved = await Save(c, d); await saved.Variants.Count.Should().BeEqualTo(100);
        saved.Variants.Add(new()); await ((int)(await Send(c, saved)).StatusCode).Should().BeEqualTo(422);
        await (await Read(c)).Variants.Count.Should().BeEqualTo(100);
    }
    [Test]
    public async Task ConcurrentCatalogSavesHaveOneWinnerAndDetailsPreserveIdentifiersPricesStock()
    {
        using var c = AdminClient; var d = await Read(c); var before = ProductEditorRules.Copy(d.Variants[0]); d.Information.Name += " Updated";
        var results = await Task.WhenAll(Send(c, d), Send(c, d));
        await results.Count(r => r.IsSuccessStatusCode).Should().BeEqualTo(1); await results.Count(r => (int)r.StatusCode == 409).Should().BeEqualTo(1);
        var after = (await Read(c)).Variants.Single(); await after.Id.Should().BeEqualTo(before.Id); await after.Sku.Should().BeEqualTo(before.Sku); await after.Price.Should().BeEqualTo(before.Price); await after.OnHand.Should().BeEqualTo(before.OnHand);
    }
    [Test]
    public async Task StockUsesXminReservationsAndIndependentCatalogRevisionWithIdempotentLedger()
    {
        using var c = AdminClient; var d = await Read(c); var v = d.Variants[0];
        await using (var scope = CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryItems.Where(i => i.ProductVariantId == v.Id).ExecuteUpdateAsync(u => u.SetProperty(i => i.QuantityReserved, 3));
        var stale = new EditorStock(Guid.NewGuid(), d.Id, v.Id, v.InventoryVersion, v.OnHand + 2, "Stock recount");
        await ((int)(await c.PostAsJsonAsync($"{Root}/{d.Id}/stock", stale)).StatusCode).Should().BeEqualTo(409);
        v = (await Read(c)).Variants[0];
        var invalid = stale with { OperationId = Guid.NewGuid(), ExpectedVersion = v.InventoryVersion, Quantity = 2 };
        await ((int)(await c.PostAsJsonAsync($"{Root}/{d.Id}/stock", invalid)).StatusCode).Should().BeEqualTo(409);
        var valid = stale with { OperationId = Guid.NewGuid(), ExpectedVersion = v.InventoryVersion };
        var responses = await Task.WhenAll(c.PostAsJsonAsync($"{Root}/{d.Id}/stock", valid), c.PostAsJsonAsync($"{Root}/{d.Id}/stock", valid));
        await responses.All(r => r.IsSuccessStatusCode).Should().BeTrue();
        var fresh = await Read(c); await fresh.Revision.Should().BeEqualTo(d.Revision); await fresh.Variants[0].Reserved.Should().BeEqualTo(3);
        await fresh.Variants[0].OnHand.Should().BeEqualTo(v.OnHand + 2);
        d.Information.Description = "Catalog changes after stock movement"; await (await Save(c, d)).Variants[0].OnHand.Should().BeEqualTo(v.OnHand + 2);
        await using var check = CreateScope(); await (await check.ServiceProvider.GetRequiredService<AppDbContext>().InventoryTransactions.CountAsync(t => t.Note == "Stock recount")).Should().BeEqualTo(1);
    }
    [Test]
    public async Task ProductMetadataIsIndependentAndLabelsDoNotChangeCodesOrSku()
    {
        using var c = AdminClient; var d = await Read(c); var a = await Axis(); a.Fields = [new("hex", "Swatch", ValueFieldKind.HexColor), new("capacity", "Capacity", ValueFieldKind.Number)]; a.SwatchField = "hex"; a.Options[0].Metadata = new() { ["hex"] = "#ffffff", ["capacity"] = "2.5" }; Map(d, a);
        var saved = await Save(c, d); var code = saved.Attributes[0].Options[0].Code; var sku = saved.Variants[0].Sku;
        saved.Attributes[0].Options[0].Value = "Midnight Black"; saved.Information.Name = "Renamed product";
        saved = await Save(c, saved); await saved.Attributes[0].Options[0].Code.Should().BeEqualTo(code); await saved.Variants[0].Sku.Should().BeEqualTo(sku);
        var html = await c.GetStringAsync($"/product/{saved.Information.Slug}"); await html.Contains("background-color:#FFFFFF").Should().BeTrue(); await html.Contains("Midnight Black").Should().BeTrue(); await html.Contains("capacity").Should().BeFalse();
        var other = await Read(c, 2); var copy = ProductEditorRules.Copy(saved.Attributes[0]); foreach (var value in copy.Options) { value.Id = 0; value.ClientId = Guid.NewGuid(); } Map(other, copy); other.Attributes[0].Options[0].Value = "Matte Black";
        await Save(c, other); await (await Read(c)).Attributes[0].Options[0].Value.Should().BeEqualTo("Midnight Black");
        saved.Attributes[0].Fields.Clear(); saved.Attributes[0].SwatchField = null; await ((int)(await Send(c, saved)).StatusCode).Should().BeEqualTo(422);
        saved.Attributes[0].Fields = a.Fields; saved.Attributes[0].SwatchField = "hex"; saved.Attributes[0].Options[0].Metadata["hex"] = "#fff";
        await ((int)(await Send(c, saved)).StatusCode).Should().BeEqualTo(422);
        saved.Attributes[0].Options[0].Metadata = new() { ["hex"] = "", ["capacity"] = "" };
        saved.Attributes[0].Fields.Clear(); saved.Attributes[0].SwatchField = null;
        await (await Save(c, saved)).Attributes[0].Fields.Count.Should().BeEqualTo(0);
    }
    [Test]
    public async Task GlobalMetadataCopiesWithoutChangingExistingProductCopies()
    {
        using var c = AdminClient; var a = await Axis();
        await ((int)(await c.PutAsJsonAsync($"{Root}/templates/{a.DefinitionId}", new EditorTemplateData(a.DefinitionId, [new("hex", "Color", ValueFieldKind.HexColor)], "hex"))).StatusCode).Should().BeEqualTo(200);
        var suggestion = a.Options[0].SuggestionId!.Value;
        await ((int)(await c.PutAsJsonAsync($"{Root}/suggestions/{suggestion}", new EditorSuggestionData(suggestion, [], new() { ["hex"] = "#123abc" }))).StatusCode).Should().BeEqualTo(200);
        var loaded = (await c.GetFromJsonAsync<ProductEditorData>($"{Root}/1"))!; var template = loaded.Templates.Single(t => t.Id == a.DefinitionId);
        a.Fields = template.Fields; a.SwatchField = template.SwatchField; a.Options[0].Metadata = template.Suggestions[0].Metadata; Map(loaded.Draft, a); await Save(c, loaded.Draft);
        await c.PutAsJsonAsync($"{Root}/suggestions/{suggestion}", new EditorSuggestionData(suggestion, [], new() { ["hex"] = "#000000" }));
        await (await Read(c)).Attributes[0].Options[0].Metadata["hex"].Should().BeEqualTo("#123ABC");
    }
    [Test]
    public async Task SkuCollisionIsCaseInsensitiveIncludesArchivedAndRequiresExplicitResolution()
    {
        using var c = AdminClient; var d = await Read(c); var other = await Read(c, 2); d.Variants[0].Sku = " " + other.Variants[0].Sku.ToLowerInvariant() + " ";
        await ((int)(await Send(c, d)).StatusCode).Should().BeEqualTo(409);
        d = await Read(c); d.Variants[0].Sku = ""; var saved = await Save(c, d); var sku = saved.Variants[0].Sku;
        saved.Information.Name = "New title"; saved = await Save(c, saved); await saved.Variants[0].Sku.Should().BeEqualTo(sku);
        saved.Variants[0].Sku = "MANUAL-123"; await (await Save(c, saved)).Variants[0].Sku.Should().BeEqualTo("MANUAL-123");
    }
    [Test]
    public async Task MissingCombinationKeepsChoiceAndShowsNaWithoutStockAlert()
    {
        using var c = AdminClient; var d = await Read(c); var a = await Axis(); Map(d, a); d = await Save(c, d);
        var attribute = d.Attributes[0]; var html = await c.GetStringAsync($"/product/{d.Information.Slug}?color={attribute.Options[1].Code}");
        await html.Contains("SKU: N/A").Should().BeTrue(); await html.Contains("Out of stock").Should().BeTrue(); await html.Contains("Notify me").Should().BeFalse();
        await html.Contains($"color={attribute.Options[1].Code}").Should().BeTrue();
        var existing = await c.GetStringAsync($"/product/{d.Information.Slug}?color={attribute.Options[0].Code}"); await existing.Contains("SKU: N/A").Should().BeFalse();
    }
    [Test]
    public async Task ArchivedMatchesRequireExplicitRestorationAndRetiredValuesPreserveSelections()
    {
        using var c = AdminClient; var d = await Read(c); var a = await Axis(); Map(d, a);
        d.Variants.Add(new() { Choices = new() { [a.DefinitionId] = a.Options[1].ClientId } });
        d = await Save(c, d); var row = d.Variants.Single(v => v.Id != 1); var id = row.Id;
        row.Archived = true; d = await Save(c, d);
        var page = (await (await c.PostAsJsonAsync($"{Root}/preview", new EditorCombinationQuery(d))).Content.ReadFromJsonAsync<EditorCombinationPage>())!;
        var archived = page.Rows.Single(r => r.Status == "Archived"); await archived.ExistingIds.Single().Should().BeEqualTo(id);
        d.Variants.Add(new() { Choices = archived.Choices }); await ((int)(await Send(c, d)).StatusCode).Should().BeEqualTo(409); d.Variants.RemoveAt(d.Variants.Count - 1);
        d.Variants.Single(v => v.Id == id).Archived = false; d = await Save(c, d); await d.Variants.Single(v => v.Id == id).OnHand.Should().BeEqualTo(0);
        d.Variants.Single(v => v.Id == id).Archived = true; d.Attributes[0].Options[1].Retired = true; d = await Save(c, d);
        await d.Variants.Single(v => v.Id == id).Choices.Count.Should().BeEqualTo(1); await d.Attributes[0].Options[1].Retired.Should().BeTrue();
        d.Attributes[0].Options[0].Retired = true; await ((int)(await Send(c, d)).StatusCode).Should().BeEqualTo(422);
    }
    [Test]
    public async Task ForeignOptionIdsAndNormalizedDuplicateValuesNeverPartiallySave()
    {
        using var c = AdminClient; var first = await Read(c); var axis = await Axis(); Map(first, axis); first = await Save(c, first);
        var other = await Read(c, 2); var foreign = ProductEditorRules.Copy(first.Attributes[0]); Map(other, foreign);
        await ((int)(await Send(c, other)).StatusCode).Should().BeEqualTo(422); await (await Read(c, 2)).Attributes.Count.Should().BeEqualTo(0);
        first.Attributes[0].Options.Add(new() { Value = "  COLOR 0  " }); await ((int)(await Send(c, first)).StatusCode).Should().BeEqualTo(422);
        await (await Read(c)).Attributes[0].Options.Count.Should().BeEqualTo(2);
    }
    [Test]
    public async Task VariantDiscountFlowsToDetailsCatalogCartAndFeed()
    {
        using var c = AdminClient; var d = await Read(c); var v = d.Variants[0]; v.Price = 12300; v.CompareAt = 15000; d = await Save(c, d);
        await using var scope = CreateScope(); var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();
        var summary = (await catalog.LatestAsync(null, 20)).Single(p => p.Id == d.Id); await summary.Price.Should().BeEqualTo(12300); await summary.OriginalPrice.Should().BeEqualTo(15000);
        var details = (await catalog.DetailsAsync(d.Information.Slug!))!; await details.Variants.Single(v => v.Id == d.Variants[0].Id).OriginalPrice.Should().BeEqualTo(15000);
        var cart = await scope.ServiceProvider.GetRequiredService<ShoppingService>().ResolveAsync([new(d.Variants[0].Id, 2)]); await cart.Items.Single().Price.Should().BeEqualTo(12300);
        var feed = await c.GetStringAsync("/feeds/google.xml"); await feed.Contains("12300.00 BDT").Should().BeTrue(); await feed.Contains("15000.00 BDT").Should().BeTrue();
    }
    [Test]
    public async Task ZeroStockVariantKeepsRealPriceAndAlertButMissingCombinationHasNeither()
    {
        using var c = AdminClient; var d = await Read(c); var a = await Axis(); Map(d, a); d = await Save(c, d); var v = d.Variants[0];
        await c.PostAsJsonAsync($"{Root}/{d.Id}/stock", new EditorStock(Guid.NewGuid(), d.Id, v.Id, v.InventoryVersion, 0, "Sold out fixture"));
        var existing = await c.GetStringAsync($"/product/{d.Information.Slug}?color={d.Attributes[0].Options[0].Code}");
        await existing.Contains("SKU: N/A").Should().BeFalse(); await existing.Contains("Notify me").Should().BeTrue();
        var missing = await c.GetStringAsync($"/product/{d.Information.Slug}?color={d.Attributes[0].Options[1].Code}"); await missing.Contains("SKU: N/A").Should().BeTrue(); await missing.Contains("Notify me").Should().BeFalse();
    }
    [Test]
    public async Task MalformedPreviewIsRejectedWithoutWrites()
    {
        using var c = AdminClient; var d = await Read(c); var a = await Axis(); Map(d, a); a.Options[1].ClientId = a.Options[0].ClientId;
        await ((int)(await c.PostAsJsonAsync($"{Root}/preview", new EditorCombinationQuery(d))).StatusCode).Should().BeEqualTo(400);
        await (await Read(c)).Attributes.Count.Should().BeEqualTo(0);
    }
    private sealed class StrictFactory : JasWebApplicationFactory { protected override bool UseRealAntiforgery => true; }
    private static readonly StrictFactory Strict = new();
    [Test]
    public async Task EditorRequiresCurrentAdministratorRealAntiforgeryAndNoStore()
    {
        using var customer = CustomerClient; await ((int)(await customer.GetAsync($"{Root}/0")).StatusCode).Should().BeEqualTo(403);
        using var c = Strict.CreateAdminClient(); var d = await Read(c, 0); d.Information.Name = "Protected draft"; d.Information.CategoryId = 1;
        await ((int)(await Send(c, d)).StatusCode).Should().BeEqualTo(400);
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "wrong"); await ((int)(await Send(c, d)).StatusCode).Should().BeEqualTo(400); c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var security = (await c.GetFromJsonAsync<JASIntWeb.Client.Dtos.AntiforgeryDto>("/api/admin/uploads/security"))!; c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", security.Token);
        var good = await Send(c, d); await good.IsSuccessStatusCode.Should().BeTrue(); await good.Headers.CacheControl!.NoStore.Should().BeTrue();
        c.DefaultRequestHeaders.Add("X-Test-SecurityStamp", "expired"); await ((int)(await c.GetAsync($"{Root}/0")).StatusCode).Should().BeEqualTo(401);
    }
    [Test]
    public async Task LegacyMutationContractsReturnEditorUpdatedWithoutWriting()
    {
        using var c = AdminClient;
        foreach (var path in new[] { "/api/admin/products", "/api/admin/products/1/workspace/generate", "/api/admin/products/1/workspace/stock" })
        { var response = await c.PostAsJsonAsync(path, new { }); await ((int)response.StatusCode).Should().BeEqualTo(410); }
        await ((int)(await c.PutAsJsonAsync("/api/admin/products/1/workspace/configuration", new { })).StatusCode).Should().BeEqualTo(410);
        await (await Read(c)).Variants.Count.Should().BeEqualTo(1);
    }
}
