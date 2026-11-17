using JASIntWeb.Services;
namespace JASIntWeb.Tests.Unit;

public class CatalogContentTests
{
    [Test]
    public async Task CatalogContent_RendersMarkdownAndExistingHtmlWithSafeFormatting()
    {
        var renderer = new CatalogContentRenderer();
        var html = renderer.Render("## Buyer guide\n\n**Genuine** gadgets\n\n- Audio\n- Power\n\n| Size | Price |\n|---|---|\n| Small | 10 |\n\n<p>Existing <em>HTML</em></p>");
        foreach (var expected in new[] { "<h2>Buyer guide</h2>", "<strong>Genuine</strong>", "<li>Audio</li>", "<table>", "<em>HTML</em>" }) await html.Contains(expected).Should().BeTrue();
    }
    [Test]
    public async Task CatalogContent_RemovesScriptsHandlersUnsafeLinksAndEmbeds()
    {
        var html = new CatalogContentRenderer().Render("<script>alert(1)</script><iframe src='https://example.test'></iframe><p onclick='alert(1)'>Safe</p><a href='javascript:alert(1)'>bad</a>\n\n[good](https://example.test)");
        foreach (var bad in new[] { "<script", "<iframe", "onclick", "javascript:" }) await html.Contains(bad).Should().BeFalse();
        await html.Contains("Safe").Should().BeTrue(); await html.Contains("href=\"https://example.test\"").Should().BeTrue();
    }
    [Test]
    public async Task ProductValues_NormalizeUnicodeWhitespaceAndCase()
    {
        await ProductWorkspaceService.NormalizeValue("  Ｂlack  ").Should().BeEqualTo("BLACK");
        await ProductWorkspaceService.NormalizeValue("Midnight Black").Should().NotBeEqualTo(ProductWorkspaceService.NormalizeValue("Matte Black"));
    }
    [Test]
    public async Task RichContent_PreservesSafeImagesAndRemovesDangerousImageSources()
    {
        var html = new CatalogContentRenderer().Render("<p><u>Underlined</u></p><img src='/uploads/genuine.png' alt='Genuine' width='320' onerror='alert(1)'><img src='javascript:alert(1)'><img src='data:image/svg+xml,unsafe'>");
        await html.Contains("src=\"/uploads/genuine.png\"").Should().BeTrue();
        await html.Contains("width=\"320\"").Should().BeTrue();
        await html.Contains("<u>Underlined</u>").Should().BeTrue();
        foreach (var forbidden in new[] { "onerror", "javascript:", "data:image" }) await html.Contains(forbidden).Should().BeFalse();
    }
}
