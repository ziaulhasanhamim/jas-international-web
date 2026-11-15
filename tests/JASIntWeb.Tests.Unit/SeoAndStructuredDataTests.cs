using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using JASIntWeb.Services.Meta;
using JASIntWeb.Services.Seo;
using Microsoft.Extensions.Configuration;

namespace JASIntWeb.Tests.Unit;

public class SeoAndStructuredDataTests
{
    private static string GetRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "JASIntWeb.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    }

    [Test]
    public async Task CanonicalUrl_StripsQueryParametersAndNormalizesSlashes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicBaseUrl"] = "https://www.jasinternational.com.bd",
            ["Seo:SiteName"] = "JAS International"
        }).Build();

        var seoService = new SeoService(configuration);
        await seoService.PublicBaseUrl.Should().BeEqualTo("https://www.jasinternational.com.bd");

        var canon1 = seoService.BuildCanonicalUrl("/products?category=smartphones&sort=price_asc");
        await canon1.Should().BeEqualTo("https://www.jasinternational.com.bd/products");

        var canon2 = seoService.BuildCanonicalUrl("product/anker-soundcore-p20i");
        await canon2.Should().BeEqualTo("https://www.jasinternational.com.bd/product/anker-soundcore-p20i");

        var canon3 = seoService.BuildCanonicalUrl("/about/");
        await canon3.Should().BeEqualTo("https://www.jasinternational.com.bd/about");
    }

    [Test]
    public async Task SearchTitles_BuildLocalizedBangladeshIntentFormats()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicBaseUrl"] = "https://www.jasinternational.com.bd",
            ["Seo:SiteName"] = "JAS International"
        }).Build();

        var seoService = new SeoService(configuration);

        var prodTitle = seoService.BuildProductTitle("Anker Soundcore P20i", "Anker");
        await prodTitle.Should().Contain("Anker Soundcore P20i");
        await prodTitle.Should().Contain("Price in Bangladesh");
        await prodTitle.Should().Contain("JAS International");

        var catTitle = seoService.BuildCategoryTitle("TWS Earbuds");
        await catTitle.Should().Contain("TWS Earbuds Price in Bangladesh");

        var brandTitle = seoService.BuildBrandTitle("Apple");
        await brandTitle.Should().Contain("Apple Products & Price in Bangladesh");
    }

    [Test]
    public async Task SanitizeDescription_StripsHtmlTagsAndPreservesQuotes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicBaseUrl"] = "https://www.jasinternational.com.bd"
        }).Build();

        var seoService = new SeoService(configuration);
        var sanitized = seoService.SanitizeDescription("<p>Get original <strong>Anker</strong> TWS &quot;earbuds&quot; in Khulna.\n\nVisit showroom today!</p>");

        await sanitized.Should().NotContain("<");
        await sanitized.Should().NotContain(">");
        await sanitized.Should().Contain("Anker");
        await sanitized.Should().Contain("\"earbuds\"");
    }

    [Test]
    public async Task JsonLdBuilder_ProductSchema_GeneratesValidStructuredData()
    {
        var productJsonLd = JsonLdBuilder.BuildProduct(
            name: "Apple 20W USB-C \"Power Adapter\" Original",
            description: "Official 100% genuine Apple 20W fast charger for iPhone with 1-year warranty.",
            productUrl: "https://www.jasinternational.com.bd/product/apple-20w-charger",
            imageUrls: ["https://www.jasinternational.com.bd/images/apple-20w.jpg"],
            brandName: "Apple",
            sku: "APL-20W-WHT",
            gtin: "194252157015",
            mpn: null,
            price: 2850m,
            compareAtPrice: 3200m,
            inStock: true,
            ratingValue: 4.9m,
            reviewCount: 38
        );

        await string.IsNullOrWhiteSpace(productJsonLd).Should().BeFalse();

        using var doc = JsonDocument.Parse(productJsonLd);
        var root = doc.RootElement;
        await root.GetProperty("@context").GetString().Should().BeEqualTo("https://schema.org");
        await root.GetProperty("@type").GetString().Should().BeEqualTo("Product");
        await root.GetProperty("name").GetString().Should().BeEqualTo("Apple 20W USB-C \"Power Adapter\" Original");
        await root.GetProperty("offers").GetProperty("price").GetDecimal().Should().BeEqualTo(2850m);
        await root.GetProperty("offers").GetProperty("priceCurrency").GetString().Should().BeEqualTo("BDT");
        await root.GetProperty("aggregateRating").GetProperty("ratingValue").GetDecimal().Should().BeEqualTo(4.9m);
    }

    [Test]
    public async Task JsonLdBuilder_LocalBusinessSchema_GeneratesValidStructuredData()
    {
        var localBizJsonLd = JsonLdBuilder.BuildLocalBusiness(
            baseUrl: "https://www.jasinternational.com.bd",
            logoUrl: "https://www.jasinternational.com.bd/jas-logo.png",
            telephone: "+8801711465078"
        );

        using var doc = JsonDocument.Parse(localBizJsonLd);
        var root = doc.RootElement;
        await root.GetProperty("@type").GetString().Should().BeEqualTo("ElectronicsStore");
        await root.GetProperty("telephone").GetString().Should().BeEqualTo("+8801711465078");
        await root.GetProperty("address").GetProperty("addressLocality").GetString().Should().BeEqualTo("Khulna");
    }

    [Test]
    public async Task StaticPerformanceAssets_ExistInWwwroot()
    {
        var repoRoot = GetRepositoryRoot();
        var wwwroot = Path.Combine(repoRoot, "JASIntWeb", "wwwroot");

        var ogCard = Path.Combine(wwwroot, "jas-og-card.png");
        await File.Exists(ogCard).Should().BeTrue();

        var webpLogo = Path.Combine(wwwroot, "jas-logo.webp");
        await File.Exists(webpLogo).Should().BeTrue();

        var manifest = Path.Combine(wwwroot, "site.webmanifest");
        await File.Exists(manifest).Should().BeTrue();

        var favicon48 = Path.Combine(wwwroot, "favicon-48x48.png");
        await File.Exists(favicon48).Should().BeTrue();
    }

    [Test]
    public async Task ThirdPartyAdminScripts_IsolatedFromStorefrontAppRazor()
    {
        var repoRoot = GetRepositoryRoot();
        var appRazor = Path.Combine(repoRoot, "JASIntWeb", "Components", "App.razor");
        var appRazorText = await File.ReadAllTextAsync(appRazor);

        // Core Web Vitals guard: TinyMCE must never be in public App.razor
        await appRazorText.Should().NotContain("tinymce");

        var adminLayout = Path.Combine(repoRoot, "JASIntWeb", "Components", "Layout", "AdminLayout.razor");
        var adminLayoutText = await File.ReadAllTextAsync(adminLayout);
        var adminEditorJs = Path.Combine(repoRoot, "JASIntWeb", "wwwroot", "js", "modules", "admin-editor.js");
        var adminEditorJsText = await File.ReadAllTextAsync(adminEditorJs);

        await adminLayoutText.Should().Contain("admin-editor.js");
        await adminEditorJsText.Should().Contain("tinymce");
    }

    [Test]
    public async Task StaticImages_ComplyWithByteBudgets()
    {
        var repoRoot = GetRepositoryRoot();
        var wwwroot = Path.Combine(repoRoot, "JASIntWeb", "wwwroot");

        var logoPng = new FileInfo(Path.Combine(wwwroot, "jas-logo.png"));
        var logoWebp = new FileInfo(Path.Combine(wwwroot, "jas-logo.webp"));
        var logoAvif = new FileInfo(Path.Combine(wwwroot, "jas-logo.avif"));

        await logoPng.Exists.Should().BeTrue();
        await (logoPng.Length < 100 * 1024).Should().BeTrue();

        await logoWebp.Exists.Should().BeTrue();
        await (logoWebp.Length < 25 * 1024).Should().BeTrue();

        await logoAvif.Exists.Should().BeTrue();
        await (logoAvif.Length < 25 * 1024).Should().BeTrue();

        var ogPng = new FileInfo(Path.Combine(wwwroot, "jas-og-card.png"));
        var ogWebp = new FileInfo(Path.Combine(wwwroot, "jas-og-card.webp"));
        var ogAvif = new FileInfo(Path.Combine(wwwroot, "jas-og-card.avif"));

        await ogPng.Exists.Should().BeTrue();
        await (ogPng.Length < 60 * 1024).Should().BeTrue();

        await ogWebp.Exists.Should().BeTrue();
        await (ogWebp.Length < 30 * 1024).Should().BeTrue();

        await ogAvif.Exists.Should().BeTrue();
        await (ogAvif.Length < 25 * 1024).Should().BeTrue();
    }

    [Test]
    public async Task PictureImageComponent_UsedAcrossKeyStorefrontComponents()
    {
        var repoRoot = GetRepositoryRoot();

        var pictureImageCode = await File.ReadAllTextAsync(Path.Combine(repoRoot, "JASIntWeb.Client", "Components", "Common", "PictureImage.razor"));
        await pictureImageCode.Should().Contain("<picture");
        await pictureImageCode.Should().Contain("type=\"image/avif\"");
        await pictureImageCode.Should().Contain("type=\"image/webp\"");
        await pictureImageCode.Should().Contain("<img");

        var productCardCode = await File.ReadAllTextAsync(Path.Combine(repoRoot, "JASIntWeb.Client", "Components", "Home", "ProductCard.razor"));
        await productCardCode.Should().Contain("<PictureImage");

        var productDetailsCode = await File.ReadAllTextAsync(Path.Combine(repoRoot, "JASIntWeb", "Components", "Pages", "ProductDetails.razor"));
        await productDetailsCode.Should().Contain("<PictureImage");

        var siteHeaderCode = await File.ReadAllTextAsync(Path.Combine(repoRoot, "JASIntWeb.Client", "Components", "Layout", "SiteHeader.razor"));
        await siteHeaderCode.Should().Contain("<PictureImage");

        var siteFooterCode = await File.ReadAllTextAsync(Path.Combine(repoRoot, "JASIntWeb", "Components", "Layout", "SiteFooter.razor"));
        await siteFooterCode.Should().Contain("<PictureImage");
    }

    [Test]
    public async Task HealthEndpointAndMonitoring_ConfiguredCorrectly()
    {
        var repoRoot = GetRepositoryRoot();
        var projectRoot = Path.Combine(repoRoot, "JASIntWeb");

        var healthEndpointsCode = await File.ReadAllTextAsync(Path.Combine(projectRoot, "Endpoints", "HealthEndpoints.cs"));
        await healthEndpointsCode.Should().Contain("MapHealthChecks");
        await healthEndpointsCode.Should().Contain("HttpMethods.IsHead");

        var programCode = await File.ReadAllTextAsync(Path.Combine(projectRoot, "Program.cs"));
        await programCode.Should().Contain("AddHealthChecks");
        await programCode.Should().Contain("AddDbContextCheck<AppDbContext>");
        await programCode.Should().Contain("AddPooledDbContextFactory<AppDbContext>");

        var canonicalMiddlewareCode = await File.ReadAllTextAsync(Path.Combine(projectRoot, "Middleware", "CanonicalHostAndRedirectMiddleware.cs"));
        await canonicalMiddlewareCode.Should().Contain("path.Equals(\"/health\"");
    }

    [Test]
    public async Task VisitorTrackingAndAnalytics_InfrastructureConfigured()
    {
        var repoRoot = GetRepositoryRoot();
        var projectRoot = Path.Combine(repoRoot, "JASIntWeb");

        var programCode = await File.ReadAllTextAsync(Path.Combine(projectRoot, "Program.cs"));
        await programCode.Should().Contain("IVisitorAnalyticsQueue");
        await programCode.Should().Contain("VisitorAnalyticsBackgroundWorker");
        await programCode.Should().Contain("IVisitorAnalyticsService");

        var appRazorCode = await File.ReadAllTextAsync(Path.Combine(projectRoot, "Components", "App.razor"));
        await appRazorCode.Should().Contain("js/modules/analytics.js");

        var adminNavCode = await File.ReadAllTextAsync(Path.Combine(projectRoot, "Components", "Admin", "AdminNavigation.razor"));
        await adminNavCode.Should().Contain("/admin/visitors");

        var visitorsAdminCode = await File.ReadAllTextAsync(Path.Combine(projectRoot, "Components", "Pages", "Admin", "Visitors", "VisitorsAdmin.razor"));
        await visitorsAdminCode.Should().Contain("@page \"/admin/visitors\"");
    }
}
