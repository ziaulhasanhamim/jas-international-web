using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JASIntWeb.Tests.Integration;

[NotInParallel("web-integration")]
public class SitemapAndRobotsIntegrationTests
{
    private static readonly WebApplicationFactory<Program> Factory = new();

    [Test]
    public async Task GetSitemapXml_ReturnsOkWithXmlContent()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/sitemap.xml");

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await response.Content.Headers.ContentType!.MediaType.Should().BeEqualTo("application/xml");

        var content = await response.Content.ReadAsStringAsync();
        await content.Should().Contain("<urlset");
        await content.Should().Contain("/category/");
        await content.Should().Contain("/product/");
    }

    [Test]
    public async Task GetRobotsTxt_ReturnsOkWithDisallowAdminAndSitemap()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/robots.txt");

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        var content = await response.Content.ReadAsStringAsync();
        await content.Should().Contain("User-agent: *");
        await content.Should().Contain("Disallow: /admin/");
        await content.Should().Contain("Disallow: /checkout");
        await content.Should().Contain("Sitemap:");
    }

    [Test]
    public async Task LegacyProductCategoryUrl_Returns301RedirectToCanonicalCategory()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/products/smartphones");

        await response.StatusCode.Should().BeEqualTo(HttpStatusCode.MovedPermanently);
        await response.Headers.Location!.ToString().Should().BeEqualTo("/category/smartphones");
    }
}
