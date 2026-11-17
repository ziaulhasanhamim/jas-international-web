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
}
