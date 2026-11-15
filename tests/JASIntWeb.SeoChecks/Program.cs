using System.Text.Json;
using JASIntWeb.Services.Meta;
using JASIntWeb.Services.Seo;
using Microsoft.Extensions.Configuration;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("===============================================================================");
Console.WriteLine(" JAS International - SEO & Meta Ads Production Verification Suite");
Console.WriteLine(" Canonical: https://www.jasinternational.com.bd");
Console.WriteLine("===============================================================================\n");
Console.ResetColor();

var failures = 0;
var total = 0;

void Assert(bool condition, string checkName, string details = "")
{
    total++;
    if (condition)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[PASS] ");
        Console.ResetColor();
        Console.WriteLine($"{checkName} {(string.IsNullOrWhiteSpace(details) ? "" : $"({details})")}");
    }
    else
    {
        failures++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("[FAIL] ");
        Console.ResetColor();
        Console.WriteLine($"{checkName} - {details}");
    }
}

// -----------------------------------------------------------------------------
// 1. Phone Normalization & Lowercase SHA-256 Hashing (Bangladesh E.164 without '+')
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("--- 1. Bangladesh Phone Normalization & Meta SHA-256 Hashing ---");
Console.ResetColor();

var phoneVectors = new (string Input, string ExpectedNorm)[]
{
    ("01711465078", "8801711465078"),
    ("+8801711465078", "8801711465078"),
    ("8801711465078", "8801711465078"),
    ("008801711465078", "8801711465078"),
    ("01711-465078", "8801711465078"),
    ("  +88 01711 465 078 ", "8801711465078"),
    ("01912345678", "8801912345678"),
    ("+8801312345678", "8801312345678")
};

foreach (var (input, expected) in phoneVectors)
{
    var normalized = MetaConversionsApiService.NormalizePhone(input);
    Assert(normalized == expected, $"Normalize '{input}'", $"Got: {normalized}, Expected: {expected}");
}

// Verify SHA-256 lowercase hash stability
var expectedHash = "47711265563da09946f6779ef0bf6f28ba6b8439fb06b61a983037a28bff3b1d";
var actualHash = MetaConversionsApiService.HashSha256("8801711465078");
Assert(actualHash == expectedHash && actualHash.Length == 64 && actualHash == actualHash.ToLowerInvariant(), "SHA-256 matches expected test vector", actualHash);

// -----------------------------------------------------------------------------
// 2. Canonical URL Builder & Bangladesh Title Generation
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n--- 2. Canonical URL & Bangladesh Title Construction ---");
Console.ResetColor();

var inMemorySettings = new Dictionary<string, string?>
{
    ["PublicBaseUrl"] = "https://www.jasinternational.com.bd",
    ["Seo:SiteName"] = "JAS International"
};
var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
var seoService = new SeoService(configuration);

Assert(seoService.PublicBaseUrl == "https://www.jasinternational.com.bd", "Base URL is canonical www + .com.bd", seoService.PublicBaseUrl);

var canon1 = seoService.BuildCanonicalUrl("/products?category=smartphones&sort=price_asc");
Assert(canon1 == "https://www.jasinternational.com.bd/products", "Canonical strips query params", canon1);

var canon2 = seoService.BuildCanonicalUrl("product/anker-soundcore-p20i");
Assert(canon2 == "https://www.jasinternational.com.bd/product/anker-soundcore-p20i", "Canonical adds leading slash", canon2);

var canon3 = seoService.BuildCanonicalUrl("/about/");
Assert(canon3 == "https://www.jasinternational.com.bd/about", "Canonical trims trailing slash", canon3);

var prodTitle = seoService.BuildProductTitle("Anker Soundcore P20i", "Anker");
Assert(prodTitle.Contains("Anker Soundcore P20i") && prodTitle.Contains("Price in Bangladesh") && prodTitle.Contains("JAS International"), "Product title has BD Price format", prodTitle);

var catTitle = seoService.BuildCategoryTitle("TWS Earbuds");
Assert(catTitle.Contains("TWS Earbuds Price in Bangladesh"), "Category title has BD Price format", catTitle);

var brandTitle = seoService.BuildBrandTitle("Apple");
Assert(brandTitle.Contains("Apple Products & Price in Bangladesh"), "Brand title has BD format", brandTitle);

var descSanitized = seoService.SanitizeDescription("<p>Get original <strong>Anker</strong> TWS &quot;earbuds&quot; in Khulna.\n\nVisit showroom today!</p>");
Assert(!descSanitized.Contains('<') && !descSanitized.Contains('>') && descSanitized.Contains("Anker") && descSanitized.Contains("\"earbuds\""), "HTML stripped from meta description", descSanitized);

// -----------------------------------------------------------------------------
// 3. Strongly-Typed JSON-LD Generation
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n--- 3. JSON-LD Structured Data Schema Validation ---");
Console.ResetColor();

// Test Product Schema
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

Assert(!string.IsNullOrWhiteSpace(productJsonLd), "Product JSON-LD emitted");
try
{
    using var doc = JsonDocument.Parse(productJsonLd);
    var root = doc.RootElement;
    Assert(root.GetProperty("@context").GetString() == "https://schema.org", "Context is https://schema.org");
    Assert(root.GetProperty("@type").GetString() == "Product", "Type is Product");
    Assert(root.GetProperty("name").GetString() == "Apple 20W USB-C \"Power Adapter\" Original", "Quotes escaped properly without corruption");
    Assert(root.GetProperty("offers").GetProperty("price").GetDecimal() == 2850m, "Price correctly formatted");
    Assert(root.GetProperty("offers").GetProperty("priceCurrency").GetString() == "BDT", "Currency is BDT");
    Assert(root.GetProperty("aggregateRating").GetProperty("ratingValue").GetDecimal() == 4.9m, "Rating value included");
}
catch (Exception ex)
{
    Assert(false, "Product JSON-LD parses as valid JSON", ex.Message);
}

// Test LocalBusiness Schema (Khulna Showroom)
var localBizJsonLd = JsonLdBuilder.BuildLocalBusiness(
    baseUrl: "https://www.jasinternational.com.bd",
    logoUrl: "https://www.jasinternational.com.bd/jas-logo.png",
    telephone: "+8801711465078"
);

try
{
    using var doc = JsonDocument.Parse(localBizJsonLd);
    var root = doc.RootElement;
    Assert(root.GetProperty("@type").GetString() == "ElectronicsStore", "LocalBusiness is ElectronicsStore");
    Assert(root.GetProperty("telephone").GetString() == "+8801711465078", "Telephone matches hotline");
    Assert(root.GetProperty("address").GetProperty("addressLocality").GetString() == "Khulna", "Locality is Khulna");
}
catch (Exception ex)
{
    Assert(false, "LocalBusiness JSON-LD parses as valid JSON", ex.Message);
}

// -----------------------------------------------------------------------------
// 4. Critical Performance Assets & Web Vitals
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n--- 4. Performance & Core Web Vitals Assets ---");
Console.ResetColor();

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../JASIntWeb"));
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var wwwroot = Path.Combine(projectRoot, "wwwroot");
var webRoot = wwwroot;

var ogCard = Path.Combine(wwwroot, "jas-og-card.png");
Assert(File.Exists(ogCard), "Optimized 1200x630 OG Card exists", $"Size: {new FileInfo(ogCard).Length / 1024} KB");

var webpLogo = Path.Combine(wwwroot, "jas-logo.webp");
Assert(File.Exists(webpLogo), "Next-gen WebP logo exists", $"Size: {new FileInfo(webpLogo).Length / 1024} KB");

var manifest = Path.Combine(wwwroot, "site.webmanifest");
Assert(File.Exists(manifest), "PWA WebManifest exists");

var favicon48 = Path.Combine(wwwroot, "favicon-48x48.png");
Assert(File.Exists(favicon48), "48x48 Favicon exists");

var appRazor = Path.Combine(projectRoot, "Components/App.razor");
var appRazorText = File.ReadAllText(appRazor);
Assert(!appRazorText.Contains("tinymce"), "TinyMCE is NOT loaded on public App.razor", "Big Core Web Vitals win");

var adminLayout = Path.Combine(projectRoot, "Components/Layout/AdminLayout.razor");
var adminLayoutText = File.ReadAllText(adminLayout);
var adminEditorJs = Path.Combine(wwwroot, "js/modules/admin-editor.js");
var adminEditorJsText = File.ReadAllText(adminEditorJs);
Assert(adminLayoutText.Contains("admin-editor.js") && adminEditorJsText.Contains("tinymce"), "TinyMCE is isolated to admin-editor.js on AdminLayout.razor only");

// -----------------------------------------------------------------------------
// 5. Image Compression & Modern Next-Gen Format Verification (AVIF & WebP)
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n--- 5. Image Compression & Next-Gen Formats (AVIF & WebP) ---");
Console.ResetColor();

try
{
    using var magick = new ImageMagick.MagickImage(ImageMagick.MagickColors.Crimson, 100, 100);
    magick.Format = ImageMagick.MagickFormat.WebP;
    magick.Quality = 80;
    var webpBytes = magick.ToByteArray();
    Assert(webpBytes != null && webpBytes.Length > 0, "Magick.NET encodes WebP format", $"Bytes: {webpBytes?.Length ?? 0}");

    magick.Format = ImageMagick.MagickFormat.Avif;
    magick.Quality = 75;
    var avifBytes = magick.ToByteArray();
    Assert(avifBytes != null && avifBytes.Length > 0, "Magick.NET encodes AVIF format", $"Bytes: {avifBytes?.Length ?? 0}");
}
catch (Exception ex)
{
    Console.WriteLine($"Magick.NET error: {ex.Message}");
    Assert(false, "Magick.NET format encoding check", ex.Message);
}

// 5.1 Static Asset Compression Checks
var logoPngInfo = new FileInfo(Path.Combine(webRoot, "jas-logo.png"));
var logoWebpInfo = new FileInfo(Path.Combine(webRoot, "jas-logo.webp"));
var logoAvifInfo = new FileInfo(Path.Combine(webRoot, "jas-logo.avif"));
Assert(logoPngInfo.Exists && logoPngInfo.Length < 100 * 1024, "Static jas-logo.png compressed under 100 KB", $"Size: {logoPngInfo.Length / 1024} KB");
Assert(logoWebpInfo.Exists && logoWebpInfo.Length < 25 * 1024, "Static jas-logo.webp exists and under 25 KB", $"Size: {logoWebpInfo.Length / 1024} KB");
Assert(logoAvifInfo.Exists && logoAvifInfo.Length < 25 * 1024, "Static jas-logo.avif exists and under 25 KB", $"Size: {logoAvifInfo.Length / 1024} KB");

var ogPngInfo = new FileInfo(Path.Combine(webRoot, "jas-og-card.png"));
var ogWebpInfo = new FileInfo(Path.Combine(webRoot, "jas-og-card.webp"));
var ogAvifInfo = new FileInfo(Path.Combine(webRoot, "jas-og-card.avif"));
Assert(ogPngInfo.Exists && ogPngInfo.Length < 60 * 1024, "Static jas-og-card.png compressed", $"Size: {ogPngInfo.Length / 1024} KB");
Assert(ogWebpInfo.Exists && ogWebpInfo.Length < 30 * 1024, "Static jas-og-card.webp exists", $"Size: {ogWebpInfo.Length / 1024} KB");
Assert(ogAvifInfo.Exists && ogAvifInfo.Length < 25 * 1024, "Static jas-og-card.avif exists", $"Size: {ogAvifInfo.Length / 1024} KB");

// 5.2 Next-Gen URL Resolution (ImageUrlHelper)
var localAvif = JASIntWeb.Client.Services.ImageUrlHelper.GetAvifUrl("/uploads/products/202611/earbuds.jpg");
var localWebp = JASIntWeb.Client.Services.ImageUrlHelper.GetWebpUrl("/uploads/products/202611/earbuds.jpg");
Assert(localAvif == "/uploads/products/202611/earbuds.avif", "Local upload resolves to companion .avif URL");
Assert(localWebp == "/uploads/products/202611/earbuds.webp", "Local upload resolves to companion .webp URL");

var cdnAvif = JASIntWeb.Client.Services.ImageUrlHelper.GetAvifUrl("https://res.cloudinary.com/jasint/image/upload/v1234/test.jpg");
var cdnWebp = JASIntWeb.Client.Services.ImageUrlHelper.GetWebpUrl("https://res.cloudinary.com/jasint/image/upload/v1234/test.jpg");
Assert(cdnAvif != null && cdnAvif.Contains("/upload/f_avif,q_auto/v1234/test.jpg"), "Cloudinary resolves to f_avif,q_auto transformation");
Assert(cdnWebp != null && cdnWebp.Contains("/upload/f_webp,q_auto/v1234/test.jpg"), "Cloudinary resolves to f_webp,q_auto transformation");

var unsplashAvif = JASIntWeb.Client.Services.ImageUrlHelper.GetAvifUrl("https://images.unsplash.com/photo-1234?auto=format");
var unsplashWebp = JASIntWeb.Client.Services.ImageUrlHelper.GetWebpUrl("https://images.unsplash.com/photo-1234?auto=format");
Assert(unsplashAvif != null && unsplashAvif.Contains("fm=avif"), "Unsplash CDN resolves to fm=avif parameter");
Assert(unsplashWebp != null && unsplashWebp.Contains("fm=webp"), "Unsplash CDN resolves to fm=webp parameter");

// 5.3 Picture Tag Implementation in Storefront Components
var pictureImageCode = File.ReadAllText(Path.Combine(repoRoot, "JASIntWeb.Client", "Components", "Common", "PictureImage.razor"));
Assert(pictureImageCode.Contains("<picture") && pictureImageCode.Contains("type=\"image/avif\"") && pictureImageCode.Contains("type=\"image/webp\"") && pictureImageCode.Contains("<img"),
    "PictureImage component generates <picture> with AVIF, WebP and standard <img> fallback");

var productCardCode = File.ReadAllText(Path.Combine(repoRoot, "JASIntWeb.Client", "Components", "Home", "ProductCard.razor"));
Assert(productCardCode.Contains("<PictureImage"), "ProductCard uses <PictureImage> with AVIF/WebP fallback");

var productDetailsCode = File.ReadAllText(Path.Combine(repoRoot, "JASIntWeb", "Components", "Pages", "ProductDetails.razor"));
Assert(productDetailsCode.Contains("<PictureImage"), "ProductDetails uses <PictureImage> with AVIF/WebP fallback");

var siteHeaderCode = File.ReadAllText(Path.Combine(repoRoot, "JASIntWeb.Client", "Components", "Layout", "SiteHeader.razor"));
Assert(siteHeaderCode.Contains("<PictureImage"), "SiteHeader uses <PictureImage> for brand logo");

var siteFooterCode = File.ReadAllText(Path.Combine(repoRoot, "JASIntWeb", "Components", "Layout", "SiteFooter.razor"));
Assert(siteFooterCode.Contains("<PictureImage"), "SiteFooter uses <PictureImage> for footer brand logo");

// -----------------------------------------------------------------------------
// 6. Health Endpoint Multi-Method (GET & HEAD) and Redirect Bypass
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n--- 6. Health Endpoint & Cloud Uptime Monitoring ---");
Console.ResetColor();

var healthEndpointsCode = File.ReadAllText(Path.Combine(projectRoot, "Endpoints", "HealthEndpoints.cs"));
Assert(healthEndpointsCode.Contains("MapHealthChecks"), "Health endpoint uses built-in .NET MapHealthChecks");
Assert(healthEndpointsCode.Contains("HttpMethods.IsHead"), "Health endpoint handles HEAD without emitting body");

var programCode = File.ReadAllText(Path.Combine(projectRoot, "Program.cs"));
Assert(programCode.Contains("AddHealthChecks") && programCode.Contains("AddDbContextCheck<AppDbContext>"), "Built-in AddHealthChecks registers EF Core database connectivity check");
Assert(programCode.Contains("AddPooledDbContextFactory<AppDbContext>"), "EF Core uses AddPooledDbContextFactory to pool context instances and reduce allocations");

var canonicalMiddlewareCode = File.ReadAllText(Path.Combine(projectRoot, "Middleware", "CanonicalHostAndRedirectMiddleware.cs"));
Assert(canonicalMiddlewareCode.Contains("path.Equals(\"/health\""), "Health check bypasses canonical host 301 redirection (Render/K8s/cloud probes)");

// -----------------------------------------------------------------------------
// 7. Industry-Standard Visitor Analytics & Tracking System
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n--- 7. Industry-Standard Visitor Analytics & Tracking System ---");
Console.ResetColor();

Assert(programCode.Contains("IVisitorAnalyticsQueue") && programCode.Contains("VisitorAnalyticsBackgroundWorker"), "Visitor analytics uses non-blocking channel queue and background worker");
Assert(programCode.Contains("IVisitorAnalyticsService") && programCode.Contains("VisitorAnalyticsService"), "Visitor analytics service registered for admin reporting");

var appRazorCode = File.ReadAllText(Path.Combine(projectRoot, "Components", "App.razor"));
Assert(appRazorCode.Contains("js/modules/analytics.js"), "Client-side tracking script registered in App.razor");

var adminNavCode = File.ReadAllText(Path.Combine(projectRoot, "Components", "Admin", "AdminNavigation.razor"));
Assert(adminNavCode.Contains("/admin/visitors"), "Live Traffic & Visitors menu item present in AdminNavigation.razor");

var visitorsAdminCode = File.ReadAllText(Path.Combine(projectRoot, "Components", "Pages", "Admin", "Visitors", "VisitorsAdmin.razor"));
Assert(visitorsAdminCode.Contains("@page \"/admin/visitors\""), "Admin visitors page route mapped at /admin/visitors");

// -----------------------------------------------------------------------------
// Summary & Exit
// -----------------------------------------------------------------------------
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("===============================================================================");
if (failures == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($" ALL {total} SEO & META ADS VERIFICATION CHECKS PASSED SUCCESSFULLY!");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($" {failures} OF {total} CHECKS FAILED!");
}
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("===============================================================================");
Console.ResetColor();

return failures == 0 ? 0 : 1;
