using System.Text;
using System.Xml.Linq;
using JASIntWeb.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Endpoints;

public static class MetaFeedEndpoints
{
    private static readonly XNamespace G = "http://base.google.com/ns/1.0";

    public static IEndpointRouteBuilder MapMetaFeedEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/feeds/meta-products.xml", async (HttpContext context, IServiceScopeFactory scopeFactory) =>
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host.Value}";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var inventoryMap = await db.InventoryItems.AsNoTracking()
                .ToDictionaryAsync(i => i.ProductVariantId, i => i.AvailableQuantity);

            var variants = await db.ProductVariants.AsNoTracking()
                .Include(v => v.Product).ThenInclude(p => p!.Brand)
                .Include(v => v.Product).ThenInclude(p => p!.Category)
                .Include(v => v.Product).ThenInclude(p => p!.ProductImages)
                .Where(v => v.IsActive && !v.IsDeleted && v.Product != null && !v.Product.IsDeleted)
                .OrderBy(v => v.ProductId)
                .ThenBy(v => v.Id)
                .ToListAsync();

            var channel = new XElement("channel",
                new XElement("title", "JAS International · Meta Advantage+ Catalog"),
                new XElement("link", baseUrl),
                new XElement("description", "Dynamic product catalog feed for Meta Ads, Instagram Shopping & Facebook Shops")
            );

            foreach (var v in variants)
            {
                var prod = v.Product!;
                var brandName = prod.Brand?.Name ?? "JAS International";
                var availableStock = inventoryMap.TryGetValue(v.Id, out var qty) ? qty : 0;
                var inStock = availableStock > 0;

                var prodImage = prod.ProductImages?.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl
                                ?? prod.MainImageUrl
                                ?? "/jas-logo.png";

                if (!prodImage.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !prodImage.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    prodImage = baseUrl.TrimEnd('/') + (prodImage.StartsWith('/') ? prodImage : "/" + prodImage);
                }

                var productUrl = $"{baseUrl}product/{prod.Slug}?variant={v.Id}";
                var title = string.IsNullOrWhiteSpace(v.OptionSignature) || v.OptionSignature.Equals("Default", StringComparison.OrdinalIgnoreCase)
                    ? prod.Name
                    : $"{prod.Name} - {v.OptionSignature}";

                var description = !string.IsNullOrWhiteSpace(prod.Description)
                    ? prod.Description.Trim()
                    : title;

                // Strip HTML tags for clean description
                description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", string.Empty);
                if (description.Length > 4900) description = description[..4900];

                var item = new XElement("item",
                    new XElement(G + "id", !string.IsNullOrWhiteSpace(v.SKU) ? v.SKU : $"V{v.Id}"),
                    new XElement(G + "item_group_id", $"P{prod.Id}"),
                    new XElement(G + "title", new XCData(title)),
                    new XElement(G + "description", new XCData(description)),
                    new XElement(G + "link", productUrl),
                    new XElement(G + "image_link", prodImage),
                    new XElement(G + "brand", brandName),
                    new XElement(G + "condition", "new"),
                    new XElement(G + "availability", inStock ? "in stock" : "out of stock"),
                    new XElement(G + "price", $"{v.SellingPrice:0.00} BDT")
                );

                if (!string.IsNullOrWhiteSpace(prod.Category?.Name))
                {
                    item.Add(new XElement(G + "product_type", prod.Category.Name));
                }

                channel.Add(item);
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("rss",
                    new XAttribute(XNamespace.Xmlns + "g", G.NamespaceName),
                    new XAttribute("version", "2.0"),
                    channel
                )
            );

            var sb = new StringBuilder();
            using (var writer = new System.IO.StringWriter(sb))
            {
                doc.Save(writer);
            }

            return Results.Content(sb.ToString(), "application/xml; charset=utf-8", Encoding.UTF8);
        }).CacheOutput(p => p.Expire(TimeSpan.FromHours(1)).Tag("catalog"));

        endpoints.MapGet("/api/feeds/meta-products.csv", async (HttpContext context, IServiceScopeFactory scopeFactory) =>
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host.Value}";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var inventoryMap = await db.InventoryItems.AsNoTracking()
                .ToDictionaryAsync(i => i.ProductVariantId, i => i.AvailableQuantity);

            var variants = await db.ProductVariants.AsNoTracking()
                .Include(v => v.Product).ThenInclude(p => p!.Brand)
                .Include(v => v.Product).ThenInclude(p => p!.Category)
                .Include(v => v.Product).ThenInclude(p => p!.ProductImages)
                .Where(v => v.IsActive && !v.IsDeleted && v.Product != null && !v.Product.IsDeleted)
                .OrderBy(v => v.ProductId)
                .ThenBy(v => v.Id)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("id,title,description,availability,condition,price,link,image_link,brand,item_group_id");

            foreach (var v in variants)
            {
                var prod = v.Product!;
                var brandName = prod.Brand?.Name ?? "JAS International";
                var availableStock = inventoryMap.TryGetValue(v.Id, out var qty) ? qty : 0;
                var inStock = availableStock > 0;

                var prodImage = prod.ProductImages?.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl
                                ?? prod.MainImageUrl
                                ?? "/jas-logo.png";

                if (!prodImage.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !prodImage.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    prodImage = baseUrl.TrimEnd('/') + (prodImage.StartsWith('/') ? prodImage : "/" + prodImage);
                }

                var productUrl = $"{baseUrl}product/{prod.Slug}?variant={v.Id}";
                var id = !string.IsNullOrWhiteSpace(v.SKU) ? v.SKU : $"V{v.Id}";
                var title = string.IsNullOrWhiteSpace(v.OptionSignature) || v.OptionSignature.Equals("Default", StringComparison.OrdinalIgnoreCase)
                    ? prod.Name
                    : $"{prod.Name} - {v.OptionSignature}";

                var description = !string.IsNullOrWhiteSpace(prod.Description) ? prod.Description : title;
                description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", string.Empty);
                if (description.Length > 2000) description = description[..2000];

                var availability = inStock ? "in stock" : "out of stock";
                var price = $"{v.SellingPrice:0.00} BDT";
                var itemGroupId = $"P{prod.Id}";

                sb.AppendLine($"{CsvEscape(id)},{CsvEscape(title)},{CsvEscape(description)},{availability},new,{price},{CsvEscape(productUrl)},{CsvEscape(prodImage)},{CsvEscape(brandName)},{CsvEscape(itemGroupId)}");
            }

            return Results.Content(sb.ToString(), "text/csv; charset=utf-8", Encoding.UTF8);
        }).CacheOutput(p => p.Expire(TimeSpan.FromHours(1)).Tag("catalog"));

        return endpoints;
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        var escaped = value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
        return $"\"{escaped}\"";
    }
}
