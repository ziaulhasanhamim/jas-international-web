using System.Text;
using JASIntWeb.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Endpoints;

public static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/sitemap.xml", async (HttpContext context, IServiceScopeFactory scopeFactory) =>
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host.Value}";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var products = await db.Products.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Select(p => new
                {
                    p.Slug,
                    LastMod = p.UpdatedAt ?? p.CreatedAt
                })
                .ToListAsync();

            var categories = await db.Categories.AsNoTracking()
                .Select(c => c.Slug)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // Homepage
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}</loc>");
            sb.AppendLine("    <changefreq>daily</changefreq>");
            sb.AppendLine("    <priority>1.0</priority>");
            sb.AppendLine("  </url>");

            // Static Pages
            var staticPages = new[] { "products", "about", "contact", "faq", "terms", "privacy", "warranty-returns" };
            foreach (var page in staticPages)
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}{page}</loc>");
                sb.AppendLine("    <changefreq>weekly</changefreq>");
                sb.AppendLine("    <priority>0.7</priority>");
                sb.AppendLine("  </url>");
            }

            // Category Archives
            foreach (var cat in categories)
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}category/{cat}</loc>");
                sb.AppendLine("    <changefreq>daily</changefreq>");
                sb.AppendLine("    <priority>0.8</priority>");
                sb.AppendLine("  </url>");
            }

            // Products
            foreach (var prod in products)
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}product/{prod.Slug}</loc>");
                sb.AppendLine($"    <lastmod>{prod.LastMod:yyyy-MM-dd}</lastmod>");
                sb.AppendLine("    <changefreq>daily</changefreq>");
                sb.AppendLine("    <priority>0.9</priority>");
                sb.AppendLine("  </url>");
            }

            sb.AppendLine("</urlset>");

            return Results.Content(sb.ToString(), "application/xml", Encoding.UTF8);
        });

        endpoints.MapGet("/robots.txt", (HttpContext context) =>
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host.Value}";
            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Disallow: /admin/");
            sb.AppendLine("Disallow: /payment-verification");
            sb.AppendLine("Disallow: /checkout");
            sb.AppendLine("Disallow: /cart");
            sb.AppendLine("Disallow: /account/");
            sb.AppendLine("Disallow: /api/");
            sb.AppendLine("Allow: /");
            sb.AppendLine();
            sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");

            return Results.Content(sb.ToString(), "text/plain", Encoding.UTF8);
        });

        return endpoints;
    }
}
