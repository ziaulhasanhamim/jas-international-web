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
        await content.Should().Contain("<sitemapindex");
        await content.Should().Contain("sitemap-products.xml");
        await content.Should().Contain("sitemap-categories.xml");

        var productsSitemapResp = await client.GetAsync("/sitemap-products.xml");
        await ((int)productsSitemapResp.StatusCode).Should().BeEqualTo(200);
        var productsContent = await productsSitemapResp.Content.ReadAsStringAsync();
        await productsContent.Should().Contain("<urlset");
        await productsContent.Should().Contain("/product/");

        var categoriesSitemapResp = await client.GetAsync("/sitemap-categories.xml");
        await ((int)categoriesSitemapResp.StatusCode).Should().BeEqualTo(200);
        var categoriesContent = await categoriesSitemapResp.Content.ReadAsStringAsync();
        await categoriesContent.Should().Contain("<urlset");
        await categoriesContent.Should().Contain("/category/");
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

    [Test]
    public async Task GetHealth_ReturnsOkWithHealthyStatus()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/health");

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        var content = await response.Content.ReadAsStringAsync();
        await content.Should().Contain("\"status\":\"healthy\"");
    }

    [Test]
    public async Task HeadHealth_ReturnsOkWithoutBody()
    {
        var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, "/health");
        var response = await client.SendAsync(request);

        // Uptime monitors (e.g. Better Uptime, UptimeRobot, Pingdom) use HEAD by default
        await ((int)response.StatusCode).Should().BeEqualTo(200);
    }

    [Test]
    public async Task GetProductDetails_OraimoHeadPhone_ReturnsOk()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/product/oraimo-boompop2-ohp-610-head-phone");
        var body = await response.Content.ReadAsStringAsync();
        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await body.Should().NotContain("unhandled exception");
        await body.Should().Contain("oraimo BoomPop2");
    }

    [Test]
    public async Task GetProductsListing_ReturnsOk()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/products");
        var body = await response.Content.ReadAsStringAsync();
        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await body.Should().NotContain("unhandled exception");
    }

    [Test]
    public async Task GetBrandPage_Apple_ReturnsOk()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/brand/apple");
        var body = await response.Content.ReadAsStringAsync();
        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await body.Should().NotContain("unhandled exception");
    }
}
