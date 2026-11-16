using System.Net;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class SitemapAndRobotsIntegrationTests : JasIntegrationTestBase
{

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

    [Test]
    public async Task GetAllStorefrontPages_ReturnOkWithoutExceptions()
    {
        var client = Factory.CreateClient();
        string[] publicRoutes =
        [
            "/",
            "/products",
            "/cart",
            "/wishlist",
            "/about",
            "/contact",
            "/faq",
            "/terms",
            "/privacy",
            "/warranty-returns",
            "/showroom",
            "/track-order",
            "/guides"
        ];

        foreach (var route in publicRoutes)
        {
            var response = await client.GetAsync(route);
            var body = await response.Content.ReadAsStringAsync();
            await ((int)response.StatusCode).Should().BeEqualTo(200);
            await body.Should().NotContain("unhandled exception");
        }
    }

    [Test]
    public async Task VisitorAnalyticsService_PostgresQueries_ExecuteWithoutErrors()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JASIntWeb.Persistence.AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<JASIntWeb.Services.Analytics.IVisitorAnalyticsService>();

        db.VisitorLogs.Add(new JASIntWeb.Persistence.Entities.VisitorLog
        {
            VisitorId = "test-vid-1",
            SessionId = "test-sid-1",
            Path = "/product/sample-1",
            Title = "Sample Product",
            City = "Khulna",
            Country = "Bangladesh",
            DeviceType = "Mobile",
            Browser = "Chrome",
            OperatingSystem = "Android",
            ReferrerDomain = "google.com",
            UtmCampaign = "spring_sale",
            UtmSource = "facebook",
            UtmMedium = "cpc",
            Timestamp = DateTimeOffset.UtcNow
        });
        db.VisitorLogs.Add(new JASIntWeb.Persistence.Entities.VisitorLog
        {
            VisitorId = "test-vid-2",
            SessionId = "test-sid-2",
            Path = "/cart",
            Title = "Shopping Cart",
            City = "Dhaka",
            Country = "Bangladesh",
            DeviceType = "Desktop",
            Browser = "Firefox",
            OperatingSystem = "Windows",
            Timestamp = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // 1. Normal period
        var dataToday = await svc.GetDashboardDataAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        await dataToday.Should().NotBeNull();
        await dataToday.Summary.TotalPageviews.Should().BeGreaterThanOrEqualTo(2);

        // 2. All time (DateTimeOffset.MinValue / MaxValue check)
        var dataAll = await svc.GetDashboardDataAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        await dataAll.Should().NotBeNull();

        // 3. Live count & recent
        var live = await svc.GetLiveVisitorCountAsync();
        await live.Should().BeGreaterThanOrEqualTo(1);

        var recent = await svc.GetRecentActivityAsync(10);
        await recent.Should().NotBeEmpty();
    }

    [Test]
    public async Task HomePage_IsStaticSSR_HasNoInteractiveBlazorMarkers()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await body.Should().NotContain("unhandled exception");
        // Verify no interactive Server/WASM blazor markers on homepage
        await body.Should().NotContain("<!--blazor:{\"type\":\"server\"");
        await body.Should().NotContain("<!--blazor:{\"type\":\"webassembly\"");
        // Verify cart badge with data-cart-count and Buy Now button
        await body.Should().Contain("data-cart-count");
        await body.Should().Contain("data-buy-now");
    }
}
