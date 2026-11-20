using System.Text;
using System.Xml.Linq;
using JASIntWeb.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Services.Seo;

public sealed class SitemapService(AppDbContext db, ISeoService seo)
{
    private static readonly XNamespace S = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace ImageNs = "http://www.google.com/schemas/sitemap-image/1.1";

    public async Task<string> GenerateSitemapIndexAsync()
    {
        var productsLastMod = await db.Products.AsNoTracking().MaxAsync(p => (DateTimeOffset?)(p.UpdatedAt ?? p.CreatedAt)) ?? new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
        var categoriesLastMod = await db.Categories.AsNoTracking().MaxAsync(c => (DateTimeOffset?)(c.UpdatedAt ?? c.CreatedAt)) ?? new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
        var brandsLastMod = await db.Brands.AsNoTracking().MaxAsync(b => (DateTimeOffset?)(b.UpdatedAt ?? b.CreatedAt)) ?? new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
        var guidesLastMod = await db.Guides.AsNoTracking().MaxAsync(g => (DateTimeOffset?)(g.UpdatedAt ?? g.PublishedAt)) ?? new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(S + "sitemapindex",
                CreateSitemapEntry("sitemap-pages.xml", productsLastMod),
                CreateSitemapEntry("sitemap-categories.xml", categoriesLastMod),
                CreateSitemapEntry("sitemap-brands.xml", brandsLastMod),
                CreateSitemapEntry("sitemap-products.xml", productsLastMod),
                CreateSitemapEntry("sitemap-guides.xml", guidesLastMod)
            )
        );

        return doc.Declaration + Environment.NewLine + doc.ToString();

        XElement CreateSitemapEntry(string file, DateTimeOffset lastMod)
        {
            return new XElement(S + "sitemap",
                new XElement(S + "loc", $"{seo.PublicBaseUrl}/{file}"),
                new XElement(S + "lastmod", lastMod.ToString("yyyy-MM-dd"))
            );
        }
    }

    public string GenerateSitemapIndex()
    {
        var staticDate = "2026-11-20";
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(S + "sitemapindex",
                CreateSitemapEntry("sitemap-pages.xml", staticDate),
                CreateSitemapEntry("sitemap-categories.xml", staticDate),
                CreateSitemapEntry("sitemap-brands.xml", staticDate),
                CreateSitemapEntry("sitemap-products.xml", staticDate),
                CreateSitemapEntry("sitemap-guides.xml", staticDate)
            )
        );

        return doc.Declaration + Environment.NewLine + doc.ToString();

        XElement CreateSitemapEntry(string file, string date)
        {
            return new XElement(S + "sitemap",
                new XElement(S + "loc", $"{seo.PublicBaseUrl}/{file}"),
                new XElement(S + "lastmod", date)
            );
        }
    }

    public async Task<string> GeneratePagesSitemapAsync()
    {
        var catalogLastMod = (await db.Products.AsNoTracking().MaxAsync(p => (DateTimeOffset?)(p.UpdatedAt ?? p.CreatedAt)) ?? new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero)).ToString("yyyy-MM-dd");
        var guideLastMod = (await db.Guides.AsNoTracking().MaxAsync(g => (DateTimeOffset?)(g.UpdatedAt ?? g.PublishedAt)) ?? new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero)).ToString("yyyy-MM-dd");

        var staticPages = new (string Path, string ChangeFreq, string Priority, string LastMod)[]
        {
            ("/", "daily", "1.0", catalogLastMod),
            ("/products", "daily", "0.9", catalogLastMod),
            ("/guides", "daily", "0.8", guideLastMod),
            ("/showroom", "weekly", "0.8", "2026-11-10"),
            ("/about", "monthly", "0.7", "2026-11-10"),
            ("/contact", "monthly", "0.7", "2026-11-10"),
            ("/faq", "weekly", "0.7", "2026-11-12"),
            ("/warranty-returns", "monthly", "0.6", "2026-11-15"),
            ("/terms", "monthly", "0.5", "2026-11-01"),
            ("/privacy", "monthly", "0.5", "2026-11-01")
        };

        var root = new XElement(S + "urlset");

        foreach (var (path, freq, priority, lastMod) in staticPages)
        {
            root.Add(new XElement(S + "url",
                new XElement(S + "loc", seo.BuildAbsoluteUrl(path)),
                new XElement(S + "changefreq", freq),
                new XElement(S + "priority", priority),
                new XElement(S + "lastmod", lastMod)
            ));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    public string GeneratePagesSitemap()
    {
        var staticPages = new (string Path, string ChangeFreq, string Priority, string LastMod)[]
        {
            ("/", "daily", "1.0", "2026-11-20"),
            ("/products", "daily", "0.9", "2026-11-20"),
            ("/guides", "daily", "0.8", "2026-11-20"),
            ("/showroom", "weekly", "0.8", "2026-11-10"),
            ("/about", "monthly", "0.7", "2026-11-10"),
            ("/contact", "monthly", "0.7", "2026-11-10"),
            ("/faq", "weekly", "0.7", "2026-11-12"),
            ("/warranty-returns", "monthly", "0.6", "2026-11-15"),
            ("/terms", "monthly", "0.5", "2026-11-01"),
            ("/privacy", "monthly", "0.5", "2026-11-01")
        };

        var root = new XElement(S + "urlset");

        foreach (var (path, freq, priority, lastMod) in staticPages)
        {
            root.Add(new XElement(S + "url",
                new XElement(S + "loc", seo.BuildAbsoluteUrl(path)),
                new XElement(S + "changefreq", freq),
                new XElement(S + "priority", priority),
                new XElement(S + "lastmod", lastMod)
            ));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    public async Task<string> GenerateCategoriesSitemapAsync()
    {
        var categories = await db.Categories.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Slug,
                LastMod = c.UpdatedAt ?? c.CreatedAt
            })
            .ToListAsync();

        var root = new XElement(S + "urlset");

        foreach (var c in categories)
        {
            root.Add(new XElement(S + "url",
                new XElement(S + "loc", seo.BuildAbsoluteUrl($"/category/{c.Slug}")),
                new XElement(S + "changefreq", "daily"),
                new XElement(S + "priority", "0.8"),
                new XElement(S + "lastmod", c.LastMod.ToString("yyyy-MM-dd"))
            ));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    public async Task<string> GenerateBrandsSitemapAsync()
    {
        var brands = await db.Brands.AsNoTracking()
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.Name)
            .Select(b => new
            {
                b.Slug,
                LastMod = b.UpdatedAt ?? b.CreatedAt
            })
            .ToListAsync();

        var root = new XElement(S + "urlset");

        foreach (var b in brands)
        {
            root.Add(new XElement(S + "url",
                new XElement(S + "loc", seo.BuildAbsoluteUrl($"/brand/{b.Slug}")),
                new XElement(S + "changefreq", "weekly"),
                new XElement(S + "priority", "0.8"),
                new XElement(S + "lastmod", b.LastMod.ToString("yyyy-MM-dd"))
            ));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    public async Task<string> GenerateProductsSitemapAsync()
    {
        var products = await db.Products.AsNoTracking()
            .Include(p => p.ProductImages)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Name,
                p.Slug,
                p.MainImageUrl,
                Images = p.ProductImages.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList(),
                LastMod = p.UpdatedAt ?? p.CreatedAt
            })
            .ToListAsync();

        var root = new XElement(S + "urlset", new XAttribute(XNamespace.Xmlns + "image", ImageNs.NamespaceName));

        foreach (var p in products)
        {
            var urlElement = new XElement(S + "url",
                new XElement(S + "loc", seo.BuildAbsoluteUrl($"/product/{p.Slug}")),
                new XElement(S + "changefreq", "daily"),
                new XElement(S + "priority", "0.9"),
                new XElement(S + "lastmod", p.LastMod.ToString("yyyy-MM-dd"))
            );

            // Add images for Google Image Search
            var imageSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(p.MainImageUrl))
            {
                imageSet.Add(seo.BuildAbsoluteUrl(p.MainImageUrl));
            }
            foreach (var img in p.Images)
            {
                if (!string.IsNullOrWhiteSpace(img))
                {
                    imageSet.Add(seo.BuildAbsoluteUrl(img));
                }
            }

            foreach (var imgUrl in imageSet.Take(10))
            {
                urlElement.Add(new XElement(ImageNs + "image",
                    new XElement(ImageNs + "loc", imgUrl),
                    new XElement(ImageNs + "title", p.Name)
                ));
            }

            root.Add(urlElement);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    public async Task<string> GenerateGuidesSitemapAsync()
    {
        var guides = await db.Guides.AsNoTracking()
            .Where(g => g.IsPublished && !g.IsDeleted)
            .OrderByDescending(g => g.PublishedAt)
            .Select(g => new
            {
                g.Slug,
                LastMod = g.UpdatedAt ?? g.PublishedAt
            })
            .ToListAsync();

        var root = new XElement(S + "urlset");

        foreach (var g in guides)
        {
            root.Add(new XElement(S + "url",
                new XElement(S + "loc", seo.BuildAbsoluteUrl($"/guides/{g.Slug}")),
                new XElement(S + "changefreq", "weekly"),
                new XElement(S + "priority", "0.7"),
                new XElement(S + "lastmod", g.LastMod.ToString("yyyy-MM-dd"))
            ));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return doc.Declaration + Environment.NewLine + doc.ToString();
    }
}
