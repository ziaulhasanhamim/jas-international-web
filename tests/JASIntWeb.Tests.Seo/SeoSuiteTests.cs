using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using JASIntWeb.Client.Services;
using JASIntWeb.Services.Seo;
using Microsoft.Extensions.Configuration;

namespace JASIntWeb.Tests.Seo;

public class SeoSuiteTests
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

    private static ISeoService CreateSeoService(string baseUrl = "https://www.jasinternational.com.bd")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicBaseUrl"] = baseUrl,
            ["Seo:SiteName"] = "JAS International"
        }).Build();

        return new SeoService(configuration);
    }

    #region 1. Canonical URL Tests

    [Test]
    public async Task CanonicalUrl_BaseUrl_MatchesOfficialDomain()
    {
        var seo = CreateSeoService();
        await seo.PublicBaseUrl.Should().BeEqualTo("https://www.jasinternational.com.bd");
    }

    [Test]
    public async Task CanonicalUrl_StripsAllTrackingAndQueryParameters()
    {
        var seo = CreateSeoService();
        var urlsWithQueries = new[]
        {
            ("/products?utm_source=facebook&utm_medium=cpc&utm_campaign=winter_sale", "https://www.jasinternational.com.bd/products"),
            ("/products?fbclid=IwAR123456789DemoFbclid", "https://www.jasinternational.com.bd/products"),
            ("/products?gclid=Cj0KCQiA123456GoogleClickId", "https://www.jasinternational.com.bd/products"),
            ("/products?sort=price_asc&min_price=1000&max_price=5000", "https://www.jasinternational.com.bd/products"),
            ("/product/anker-p20i?variant=black&ref=homepage", "https://www.jasinternational.com.bd/product/anker-p20i")
        };

        foreach (var (input, expected) in urlsWithQueries)
        {
            var canonical = seo.BuildCanonicalUrl(input);
            await canonical.Should().BeEqualTo(expected);
        }
    }

    [Test]
    public async Task CanonicalUrl_NormalizesSlashesAndEmptyPaths()
    {
        var seo = CreateSeoService();

        await seo.BuildCanonicalUrl("/").Should().BeEqualTo("https://www.jasinternational.com.bd");
        await seo.BuildCanonicalUrl("").Should().BeEqualTo("https://www.jasinternational.com.bd");
        await seo.BuildCanonicalUrl("about/").Should().BeEqualTo("https://www.jasinternational.com.bd/about");
        await seo.BuildCanonicalUrl("/about/").Should().BeEqualTo("https://www.jasinternational.com.bd/about");
        await seo.BuildCanonicalUrl("category/tws-earbuds").Should().BeEqualTo("https://www.jasinternational.com.bd/category/tws-earbuds");
    }

    [Test]
    public async Task CanonicalUrl_PreservesValidPagination_StripsInvalidOrFirstPage()
    {
        var seo = CreateSeoService();

        // Valid page > 1 is preserved for SEO pagination
        await seo.BuildCanonicalUrl("/products?page=2").Should().BeEqualTo("https://www.jasinternational.com.bd/products?page=2");
        await seo.BuildCanonicalUrl("/products?page=5").Should().BeEqualTo("https://www.jasinternational.com.bd/products?page=5");

        // Page 1 is redundant and should be stripped to prevent duplicate canonicals
        await seo.BuildCanonicalUrl("/products?page=1").Should().BeEqualTo("https://www.jasinternational.com.bd/products");

        // Non-numeric or negative pages should be stripped
        await seo.BuildCanonicalUrl("/products?page=abc").Should().BeEqualTo("https://www.jasinternational.com.bd/products");
        await seo.BuildCanonicalUrl("/products?page=-3").Should().BeEqualTo("https://www.jasinternational.com.bd/products");
    }

    [Test]
    public async Task CanonicalUrl_StripsFragmentIdentifiers()
    {
        var seo = CreateSeoService();
        var canonical = seo.BuildCanonicalUrl("/product/apple-20w-charger#specs");
        await canonical.Should().BeEqualTo("https://www.jasinternational.com.bd/product/apple-20w-charger");
    }

    #endregion

    #region 2. Search Title & Meta Description Construction

    [Test]
    public async Task ProductTitle_ConstructsBangladeshLocalizedIntentFormat()
    {
        var seo = CreateSeoService();

        var title = seo.BuildProductTitle("Soundcore P20i", "Anker");
        await title.Should().Contain("Anker Soundcore P20i");
        await (title.Contains("Price in BD") || title.Contains("Price in Bangladesh")).Should().BeTrue();
        await title.Should().Contain("JAS International");
    }

    [Test]
    public async Task ProductTitle_AvoidsBrandNameDuplication_WhenBrandAlreadyInName()
    {
        var seo = CreateSeoService();

        var title = seo.BuildProductTitle("Apple iPhone 16 Pro Max", "Apple");
        await title.Should().NotContain("Apple Apple iPhone");
        await title.Should().Contain("Apple iPhone 16 Pro Max");
        await (title.Contains("Price in BD") || title.Contains("Price in Bangladesh")).Should().BeTrue();
    }

    [Test]
    public async Task CategoryTitle_IncludesBangladeshPriceIntent()
    {
        var seo = CreateSeoService();

        var title = seo.BuildCategoryTitle("Smart Watches");
        await title.Should().BeEqualTo("Smart Watches Price in Bangladesh | JAS International");
    }

    [Test]
    public async Task BrandTitle_IncludesProductsAndPriceFormat()
    {
        var seo = CreateSeoService();

        var title = seo.BuildBrandTitle("Samsung");
        await title.Should().BeEqualTo("Samsung Products & Price in Bangladesh | JAS International");
    }

    [Test]
    public async Task SanitizeDescription_StripsHtmlTagsAndCleansWhitespace()
    {
        var seo = CreateSeoService();

        var raw = "<p>Discover <strong>authentic</strong> gadgets in Khulna.<br>Visit our showroom &quot;JAS International&quot; today!</p>";
        var sanitized = seo.SanitizeDescription(raw);

        await sanitized.Should().NotContain("<p>");
        await sanitized.Should().NotContain("<strong>");
        await sanitized.Should().NotContain("<br>");
        await sanitized.Should().NotContain("</p>");
        await sanitized.Should().Contain("Discover authentic gadgets in Khulna.");
        await sanitized.Should().Contain("\"JAS International\"");
    }

    [Test]
    public async Task SanitizeDescription_SupportsBengaliUnicodeCharacters()
    {
        var seo = CreateSeoService();

        var bengaliRaw = "<p>খুলনায় সেরা মূল্যে আসল গ্যাজেট কিনুন। <strong>ওয়ারেন্টি সহ</strong> ডেলিভারি!</p>";
        var sanitized = seo.SanitizeDescription(bengaliRaw);

        await sanitized.Should().NotContain("<p>");
        await sanitized.Should().NotContain("<strong>");
        await sanitized.Should().Contain("খুলনায় সেরা মূল্যে আসল গ্যাজেট কিনুন। ওয়ারেন্টি সহ ডেলিভারি!");
    }

    [Test]
    public async Task SanitizeDescription_TruncatesLongDescriptionsAtWordBoundaryWithEllipsis()
    {
        var seo = CreateSeoService();
        var longText = "This is an exceptionally detailed and extensive product overview that outlines all features, hardware specifications, battery life, warranties, and accessories for tech shoppers in Khulna.";

        var sanitized = seo.SanitizeDescription(longText, maxLength: 80);

        await (sanitized.Length <= 85).Should().BeTrue();
        await sanitized.EndsWith("…").Should().BeTrue();
        await sanitized.Should().NotContain("<");
    }

    #endregion

    #region 3. JSON-LD Structured Data Schema Validation

    [Test]
    public async Task JsonLd_Product_EmitsValidSchemaOrgJson()
    {
        var jsonLd = JsonLdBuilder.BuildProduct(
            name: "Oraimo BoomPop2 Wireless Headphone",
            description: "Deep bass bluetooth headphones with 60-hour playtime and ENC mic.",
            productUrl: "https://www.jasinternational.com.bd/product/oraimo-boompop2",
            imageUrls: ["https://www.jasinternational.com.bd/images/boompop2.jpg"],
            brandName: "oraimo",
            sku: "OHP-610",
            gtin: "4895180792144",
            mpn: "BOOMPOP2-BLK",
            price: 2650m,
            compareAtPrice: 3200m,
            inStock: true,
            ratingValue: 4.8m,
            reviewCount: 42
        );

        using var doc = JsonDocument.Parse(jsonLd);
        var root = doc.RootElement;

        await root.GetProperty("@context").GetString().Should().BeEqualTo("https://schema.org");
        await root.GetProperty("@type").GetString().Should().BeEqualTo("Product");
        await root.GetProperty("name").GetString().Should().BeEqualTo("Oraimo BoomPop2 Wireless Headphone");
        await root.GetProperty("sku").GetString().Should().BeEqualTo("OHP-610");
        await root.GetProperty("gtin").GetString().Should().BeEqualTo("4895180792144");
        await root.GetProperty("mpn").GetString().Should().BeEqualTo("BOOMPOP2-BLK");

        // Offers validation
        var offers = root.GetProperty("offers");
        await offers.GetProperty("@type").GetString().Should().BeEqualTo("Offer");
        await offers.GetProperty("price").GetDecimal().Should().BeEqualTo(2650m);
        await offers.GetProperty("priceCurrency").GetString().Should().BeEqualTo("BDT");
        await offers.GetProperty("availability").GetString().Should().BeEqualTo("https://schema.org/InStock");
        await offers.GetProperty("itemCondition").GetString().Should().BeEqualTo("https://schema.org/NewCondition");

        // Brand validation
        var brand = root.GetProperty("brand");
        await brand.GetProperty("@type").GetString().Should().BeEqualTo("Brand");
        await brand.GetProperty("name").GetString().Should().BeEqualTo("oraimo");

        // Rating validation
        var rating = root.GetProperty("aggregateRating");
        await rating.GetProperty("@type").GetString().Should().BeEqualTo("AggregateRating");
        await rating.GetProperty("ratingValue").GetDecimal().Should().BeEqualTo(4.8m);
        await rating.GetProperty("reviewCount").GetInt32().Should().BeEqualTo(42);
        await rating.GetProperty("bestRating").GetInt32().Should().BeEqualTo(5);
        await rating.GetProperty("worstRating").GetInt32().Should().BeEqualTo(1);
    }

    [Test]
    public async Task JsonLd_Product_WithReviews_EmitsBothAggregateRatingAndReviewArray()
    {
        var reviews = new[]
        {
            ("Verified Buyer", 5, "Authentic smartwatch with smooth display.", DateTimeOffset.UtcNow.AddDays(-10))
        };

        var jsonLd = JsonLdBuilder.BuildProduct(
            name: "Zeblaze Btalk 4 Pro Smart Watch",
            description: "AMOLED calling smartwatch.",
            productUrl: "https://www.jasinternational.com.bd/product/zeblaze-btalk-4-pro",
            imageUrls: ["https://www.jasinternational.com.bd/images/btalk4.webp"],
            brandName: "Zeblaze",
            sku: "ZEB-BTALK4-PRO",
            gtin: null,
            mpn: "ZEB-BTALK4-PRO",
            price: 2950m,
            compareAtPrice: 3500m,
            inStock: true,
            ratingValue: 5.0m,
            reviewCount: 12,
            reviews: reviews
        );

        using var doc = JsonDocument.Parse(jsonLd);
        var root = doc.RootElement;

        var rating = root.GetProperty("aggregateRating");
        await rating.GetProperty("@type").GetString().Should().BeEqualTo("AggregateRating");
        await rating.GetProperty("ratingValue").GetDecimal().Should().BeEqualTo(5.0m);
        await rating.GetProperty("reviewCount").GetInt32().Should().BeEqualTo(12);
        await rating.GetProperty("bestRating").GetInt32().Should().BeEqualTo(5);
        await rating.GetProperty("worstRating").GetInt32().Should().BeEqualTo(1);

        var revArray = root.GetProperty("review");
        await revArray.GetArrayLength().Should().BeEqualTo(1);
        var first = revArray[0];
        await first.GetProperty("@type").GetString().Should().BeEqualTo("Review");
        await first.GetProperty("author").GetProperty("name").GetString().Should().BeEqualTo("Verified Buyer");
        await first.GetProperty("reviewRating").GetProperty("ratingValue").GetInt32().Should().BeEqualTo(5);
        await first.GetProperty("reviewRating").GetProperty("bestRating").GetInt32().Should().BeEqualTo(5);
        await first.GetProperty("reviewRating").GetProperty("worstRating").GetInt32().Should().BeEqualTo(1);
        await first.GetProperty("reviewBody").GetString().Should().BeEqualTo("Authentic smartwatch with smooth display.");
    }

    [Test]
    public async Task JsonLd_Product_OutOfStock_EmitsOutOfStockAvailability()
    {
        var jsonLd = JsonLdBuilder.BuildProduct(
            name: "Sold Out Product",
            description: "Currently unavailable item.",
            productUrl: "https://www.jasinternational.com.bd/product/sold-out",
            imageUrls: ["https://www.jasinternational.com.bd/images/sold-out.jpg"],
            brandName: "Brand",
            sku: "SKU-001",
            gtin: null,
            mpn: null,
            price: 1500m,
            compareAtPrice: null,
            inStock: false,
            ratingValue: 0,
            reviewCount: 0
        );

        using var doc = JsonDocument.Parse(jsonLd);
        var offers = doc.RootElement.GetProperty("offers");
        await offers.GetProperty("availability").GetString().Should().BeEqualTo("https://schema.org/OutOfStock");
    }

    [Test]
    public async Task JsonLd_LocalBusiness_EmitsKhulnaShowroomDetails()
    {
        var jsonLd = JsonLdBuilder.BuildLocalBusiness(
            baseUrl: "https://www.jasinternational.com.bd",
            logoUrl: "https://www.jasinternational.com.bd/jas-logo.png",
            telephone: "+8801711465078"
        );

        using var doc = JsonDocument.Parse(jsonLd);
        var root = doc.RootElement;

        await root.GetProperty("@context").GetString().Should().BeEqualTo("https://schema.org");
        await root.GetProperty("@type").GetString().Should().BeEqualTo("ElectronicsStore");
        await root.GetProperty("name").GetString().Should().BeEqualTo("JAS International");
        await root.GetProperty("telephone").GetString().Should().BeEqualTo("+8801711465078");

        var address = root.GetProperty("address");
        await address.GetProperty("@type").GetString().Should().BeEqualTo("PostalAddress");
        await address.GetProperty("addressLocality").GetString().Should().BeEqualTo("Khulna");
        await address.GetProperty("addressCountry").GetString().Should().BeEqualTo("BD");
    }

    [Test]
    public async Task JsonLd_Organization_EmitsValidSchemaOrgJson()
    {
        var jsonLd = JsonLdBuilder.BuildOrganization(
            baseUrl: "https://www.jasinternational.com.bd",
            logoUrl: "https://www.jasinternational.com.bd/jas-logo.png",
            alternateNames: ["JAS Gadgets", "JAS Tech"],
            sameAs: ["https://www.facebook.com/international.jas"]
        );

        using var doc = JsonDocument.Parse(jsonLd);
        var root = doc.RootElement;

        await root.GetProperty("@type").GetString().Should().BeEqualTo("Organization");
        await root.GetProperty("name").GetString().Should().BeEqualTo("JAS International");
        await root.GetProperty("url").GetString().Should().BeEqualTo("https://www.jasinternational.com.bd");
        await root.GetProperty("contactPoint").GetProperty("telephone").GetString().Should().BeEqualTo("+8801711465078");
    }

    [Test]
    public async Task JsonLd_WebSite_EmitsValidSearchActionSchema()
    {
        var jsonLd = JsonLdBuilder.BuildWebSite("https://www.jasinternational.com.bd");

        using var doc = JsonDocument.Parse(jsonLd);
        var root = doc.RootElement;

        await root.GetProperty("@type").GetString().Should().BeEqualTo("WebSite");
        await root.GetProperty("name").GetString().Should().BeEqualTo("JAS International");

        var searchAction = root.GetProperty("potentialAction");
        await searchAction.GetProperty("@type").GetString().Should().BeEqualTo("SearchAction");
        await searchAction.GetProperty("target").GetString().Should().Contain("{search_term_string}");
    }

    [Test]
    public async Task JsonLd_Breadcrumbs_EmitsValidHierarchyAndPositions()
    {
        var crumbs = new (string Name, string Url)[]
        {
            ("Home", "https://www.jasinternational.com.bd/"),
            ("Smartphones", "https://www.jasinternational.com.bd/category/smartphones"),
            ("iPhone 16 Pro", "https://www.jasinternational.com.bd/product/iphone-16-pro")
        };

        var jsonLd = JsonLdBuilder.BuildBreadcrumbs(crumbs);

        using var doc = JsonDocument.Parse(jsonLd);
        var root = doc.RootElement;

        await root.GetProperty("@type").GetString().Should().BeEqualTo("BreadcrumbList");
        var list = root.GetProperty("itemListElement");
        await list.GetArrayLength().Should().BeEqualTo(3);

        await list[0].GetProperty("position").GetInt32().Should().BeEqualTo(1);
        await list[0].GetProperty("name").GetString().Should().BeEqualTo("Home");

        await list[1].GetProperty("position").GetInt32().Should().BeEqualTo(2);
        await list[1].GetProperty("name").GetString().Should().BeEqualTo("Smartphones");

        await list[2].GetProperty("position").GetInt32().Should().BeEqualTo(3);
        await list[2].GetProperty("name").GetString().Should().BeEqualTo("iPhone 16 Pro");
    }

    #endregion

    #region 4. Next-Gen Image Optimization (AVIF & WebP)

    [Test]
    public async Task ImageUrlHelper_ResolvesAllSupportedCdnPatterns()
    {
        const string url = "https://media.example.test/jas-media/images/static/0123456789abcdef0123456789abcdef.webp";
        await ImageUrlHelper.GetAvifUrl(url).Should().BeEqualTo(url.Replace(".webp", ".avif"));
        await ImageUrlHelper.GetWebpUrl(url).Should().BeEqualTo(url);
        await ImageUrlHelper.GetAvifUrl("https://external.example.test/photo.jpg").Should().BeNull();

    }

    [Test]
    public async Task StaticBrandAssets_ComplyWithByteBudgets()
    {
        var repoRoot = GetRepositoryRoot();
        var wwwroot = Path.Combine(repoRoot, "JASIntWeb", "wwwroot");

        // Brand Logo
        var logoPng = new FileInfo(Path.Combine(wwwroot, "jas-logo.png"));
        var logoWebp = new FileInfo(Path.Combine(wwwroot, "jas-logo.webp"));
        var logoAvif = new FileInfo(Path.Combine(wwwroot, "jas-logo.avif"));

        await logoPng.Exists.Should().BeTrue();
        await (logoPng.Length < 100 * 1024).Should().BeTrue(); // Under 100 KB
        await logoWebp.Exists.Should().BeTrue();
        await (logoWebp.Length < 25 * 1024).Should().BeTrue(); // Under 25 KB
        await logoAvif.Exists.Should().BeTrue();
        await (logoAvif.Length < 25 * 1024).Should().BeTrue(); // Under 25 KB

        // OpenGraph Card (1200x630)
        var ogPng = new FileInfo(Path.Combine(wwwroot, "jas-og-card.png"));
        var ogWebp = new FileInfo(Path.Combine(wwwroot, "jas-og-card.webp"));
        var ogAvif = new FileInfo(Path.Combine(wwwroot, "jas-og-card.avif"));

        await ogPng.Exists.Should().BeTrue();
        await (ogPng.Length < 60 * 1024).Should().BeTrue(); // Under 60 KB
        await ogWebp.Exists.Should().BeTrue();
        await (ogWebp.Length < 30 * 1024).Should().BeTrue(); // Under 30 KB
        await ogAvif.Exists.Should().BeTrue();
        await (ogAvif.Length < 25 * 1024).Should().BeTrue(); // Under 25 KB
    }

    #endregion

    #region 5. Core Web Vitals & Script Isolation Guards

    [Test]
    public async Task HeavyAdminEditors_NeverPresentInStorefrontHeadOrBody()
    {
        var repoRoot = GetRepositoryRoot();
        var appRazor = Path.Combine(repoRoot, "JASIntWeb", "Components", "App.razor");
        var appRazorText = await File.ReadAllTextAsync(appRazor);

        // Public App.razor must never load TinyMCE or heavy WYSIWYG scripts
        await appRazorText.Should().NotContain("tinymce");
        await appRazorText.Should().NotContain("ckeditor");

        // Verify TinyMCE is strictly scoped to AdminLayout
        var adminLayout = Path.Combine(repoRoot, "JASIntWeb", "Components", "Layout", "AdminLayout.razor");
        var adminLayoutText = await File.ReadAllTextAsync(adminLayout);
        await adminLayoutText.Should().Contain("admin-editor.js");
    }

    [Test]
    public async Task PictureImageComponent_RendersStaticAvifWithWebpAndAnimationWithoutPhantomAvif()
    {
        await using var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(services, services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        const string url = "https://media.example.test/jas-media/images/static/0123456789abcdef0123456789abcdef.webp";
        async Task<string> Render(string source) => await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<JASIntWeb.Client.Components.Common.PictureImage>(Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?> { ["Src"] = source, ["Alt"] = "Fixture" }));
            return component.ToHtmlString();
        });
        var html = await Render(url);
        await html.Should().Contain($"srcset=\"{url.Replace(".webp", ".avif")}\"");
        await html.Should().Contain($"src=\"{url}\"");
        await html.Should().Contain("alt=\"Fixture\"");
        var animated = await Render(url.Replace("/static/", "/animated/"));
        await animated.Contains("image/avif").Should().BeFalse();
        await animated.Should().Contain($"src=\"{url.Replace("/static/", "/animated/")}\"");
    }

    [Test]
    public async Task EssentialStorefrontIcons_ExistInWwwroot()
    {
        var repoRoot = GetRepositoryRoot();
        var wwwroot = Path.Combine(repoRoot, "JASIntWeb", "wwwroot");

        var appleIcon = Path.Combine(wwwroot, "apple-touch-icon.png");
        var fav16 = Path.Combine(wwwroot, "favicon-16x16.png");
        var fav32 = Path.Combine(wwwroot, "favicon-32x32.png");
        var favIco = Path.Combine(wwwroot, "favicon.ico");

        await File.Exists(appleIcon).Should().BeTrue();
        await File.Exists(fav16).Should().BeTrue();
        await File.Exists(fav32).Should().BeTrue();
        await File.Exists(favIco).Should().BeTrue();
    }

    #endregion
}
