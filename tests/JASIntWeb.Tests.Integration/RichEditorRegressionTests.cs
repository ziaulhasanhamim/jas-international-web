using System.Net.Http.Json;
using System.Text.RegularExpressions;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace JASIntWeb.Tests.Integration;

public class RichEditorRegressionTests : JasIntegrationTestBase
{
    [Test]
    public async Task ProductCreation_RequiredRichEditorAndDropUploadsArePresentOnMainForm()
    {
        using var client = AdminClient;
        var html = await client.GetStringAsync("/admin/products/new");
        var labels = Regex.Matches(html, "<textarea[^>]*aria-label=\"([^\"]+)\"").Select(match => System.Net.WebUtility.HtmlDecode(match.Groups[1].Value));
        await labels.Contains("Product description & specifications").Should().BeTrue();
        await html.Contains("Drag photos here").Should().BeTrue();
        await html.Contains("Drag thumbnails to reorder").Should().BeTrue();
        var draft = new ProductEditorDraft { Information = new() { Name = "Rich product fixture", CategoryId = 1, Details = "<h2>Features</h2><p><strong>Useful</strong> details.</p>" } };
        var valid = await client.PostAsJsonAsync("/api/admin/product-editor", new EditorSave(Guid.NewGuid(), draft));
        await ((int)valid.StatusCode).Should().BeEqualTo(200);
        var saved = (await valid.Content.ReadFromJsonAsync<EditorCommit>())!;
        await saved.Editor!.Draft.Information.Details.Should().BeEqualTo(draft.Information.Details);
    }

    [Test]
    public async Task BrandSummary_FillsBlankIntroductionAndSeoUntilExplicitOverrides()
    {
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Brands.Where(b => b.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(b => b.Description, "Short brand summary.").SetProperty(b => b.IntroText, " ").SetProperty(b => b.MetaDescription, (string?)null));
        using var client = AnonymousClient;
        var slug = await db.Brands.Where(b => b.Id == 1).Select(b => b.Slug).SingleAsync();
        var html = await client.GetStringAsync("/brand/" + slug);
        await html.Contains("<p>Short brand summary.</p>").Should().BeTrue();
        await html.Contains("name=\"description\" content=\"Short brand summary.\"").Should().BeTrue();
        await db.Brands.Where(b => b.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(b => b.IntroText, "<h2>Custom introduction</h2><p>Custom visible content.</p>").SetProperty(b => b.MetaDescription, "Custom search description."));
        html = await client.GetStringAsync("/brand/" + slug);
        await html.Contains("<h2>Custom introduction</h2>").Should().BeTrue();
        await html.Contains("<p>Short brand summary.</p>").Should().BeFalse();
        await html.Contains("name=\"description\" content=\"Custom search description.\"").Should().BeTrue();
    }

    [Test]
    public async Task RichEditor_PreparesLegacyMarkdownWithoutRewritingStoredSource()
    {
        const string source = "## Legacy introduction\n\n**Original** content.";
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Brands.Where(b => b.Id == 1).ExecuteUpdateAsync(update => update.SetProperty(b => b.IntroText, source));
        using var client = AdminClient;
        var html = await client.GetStringAsync("/admin/brands/1/edit");
        var editor = Regex.Matches(html, "<textarea[^>]*>([\\s\\S]*?)</textarea>").Select(match => System.Net.WebUtility.HtmlDecode(match.Groups[1].Value)).Single(value => value.Contains("Legacy introduction"));
        await editor.Contains("<h2>Legacy introduction</h2>").Should().BeTrue();
        await editor.Contains("<strong>Original</strong>").Should().BeTrue();
        await (await db.Brands.AsNoTracking().SingleAsync(b => b.Id == 1)).IntroText.Should().BeEqualTo(source);
    }

    [Test]
    public async Task BrandAndCategory_RichHtmlPersistsAndRendersSafeImages()
    {
        using var client = AdminClient;
        const string source = "<h2>Buying advice</h2><p><strong>Genuine</strong> products.</p><p><img src=\"/jas-logo.png\" alt=\"Store logo\" onerror=\"alert(1)\"></p>";
        var brand = await client.PostAsJsonAsync("/api/admin/brands", new BrandEditForm { Name = "Rich content brand", Slug = "rich-content-brand", IntroText = source });
        await ((int)brand.StatusCode).Should().BeEqualTo(201);
        var category = await client.PostAsJsonAsync("/api/admin/categories", new CategoryEditForm { Name = "Rich content category", Slug = "rich-content-category", BottomSeoCopy = source });
        await ((int)category.StatusCode).Should().BeEqualTo(201);
        foreach (var path in new[] { "/brand/rich-content-brand", "/category/rich-content-category" })
        {
            var html = await client.GetStringAsync(path);
            await html.Contains("<h2>Buying advice</h2>").Should().BeTrue();
            await html.Contains("<strong>Genuine</strong>").Should().BeTrue();
            await html.Contains("alt=\"Store logo\"").Should().BeTrue();
            await html.Contains("onerror=\"alert(1)\"").Should().BeFalse();
        }
    }
}
