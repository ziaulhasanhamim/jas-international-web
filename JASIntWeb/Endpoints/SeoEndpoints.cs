using System.Text;
using JASIntWeb.Services.Seo;

namespace JASIntWeb.Endpoints;

public static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/sitemap.xml", (SitemapService sitemap) =>
        {
            var xml = sitemap.GenerateSitemapIndex();
            return Results.Content(xml, "application/xml", Encoding.UTF8);
        }).CacheOutput("SitemapPolicy");

        endpoints.MapGet("/sitemap-pages.xml", (SitemapService sitemap) =>
        {
            var xml = sitemap.GeneratePagesSitemap();
            return Results.Content(xml, "application/xml", Encoding.UTF8);
        }).CacheOutput("SitemapPolicy");

        endpoints.MapGet("/sitemap-categories.xml", async (SitemapService sitemap) =>
        {
            var xml = await sitemap.GenerateCategoriesSitemapAsync();
            return Results.Content(xml, "application/xml", Encoding.UTF8);
        }).CacheOutput("SitemapPolicy");

        endpoints.MapGet("/sitemap-brands.xml", async (SitemapService sitemap) =>
        {
            var xml = await sitemap.GenerateBrandsSitemapAsync();
            return Results.Content(xml, "application/xml", Encoding.UTF8);
        }).CacheOutput("SitemapPolicy");

        endpoints.MapGet("/sitemap-products.xml", async (SitemapService sitemap) =>
        {
            var xml = await sitemap.GenerateProductsSitemapAsync();
            return Results.Content(xml, "application/xml", Encoding.UTF8);
        }).CacheOutput("SitemapPolicy");

        endpoints.MapGet("/sitemap-guides.xml", async (SitemapService sitemap) =>
        {
            var xml = await sitemap.GenerateGuidesSitemapAsync();
            return Results.Content(xml, "application/xml", Encoding.UTF8);
        }).CacheOutput("SitemapPolicy");

        endpoints.MapGet("/robots.txt", (ISeoService seo) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Disallow: /admin/");
            sb.AppendLine("Disallow: /checkout");
            sb.AppendLine("Disallow: /cart");
            sb.AppendLine("Disallow: /payment");
            sb.AppendLine("Disallow: /payment-verification");
            sb.AppendLine("Disallow: /order/");
            sb.AppendLine("Disallow: /orders");
            sb.AppendLine("Disallow: /account/");
            sb.AppendLine("Disallow: /api/");
            sb.AppendLine("Disallow: /*?q=");
            sb.AppendLine("Disallow: /*?sort=");
            sb.AppendLine("Disallow: /*fbclid=");
            sb.AppendLine("Allow: /");
            sb.AppendLine();
            sb.AppendLine($"Sitemap: {seo.PublicBaseUrl}/sitemap.xml");

            return Results.Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }).CacheOutput("SitemapPolicy");

        return endpoints;
    }
}
