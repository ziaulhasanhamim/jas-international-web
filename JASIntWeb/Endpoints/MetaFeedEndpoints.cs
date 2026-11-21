using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Endpoints;

public static class MetaFeedEndpoints
{
    private static readonly XNamespace G = "http://base.google.com/ns/1.0";

    public static IEndpointRouteBuilder MapMetaFeedEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // 1. Meta XML Product Catalog Feed
        endpoints.MapGet("/feeds/meta.xml", HandleMetaXmlFeed)
            .CacheOutput(p => p.Expire(TimeSpan.FromHours(1)).Tag("catalog"));
        endpoints.MapGet("/api/feeds/meta-products.xml", HandleMetaXmlFeed)
            .CacheOutput(p => p.Expire(TimeSpan.FromHours(1)).Tag("catalog"));

        // 2. Meta CSV Product Catalog Feed
        endpoints.MapGet("/feeds/meta.csv", HandleMetaCsvFeed)
            .CacheOutput(p => p.Expire(TimeSpan.FromHours(1)).Tag("catalog"));
        endpoints.MapGet("/api/feeds/meta-products.csv", HandleMetaCsvFeed)
            .CacheOutput(p => p.Expire(TimeSpan.FromHours(1)).Tag("catalog"));

        // 3. Google Merchant Center XML Feed
        endpoints.MapGet("/feeds/google.xml", HandleGoogleMerchantXmlFeed)
            .CacheOutput(p => p.Expire(TimeSpan.FromHours(1)).Tag("catalog"));

        return endpoints;
    }

    private static async Task<IResult> ValidateTokenAsync(HttpContext context, DeliverySettingsService settingsService)
    {
        var settings = await settingsService.GetSettingsAsync();
        if (!string.IsNullOrWhiteSpace(settings.CatalogFeedToken))
        {
            var token = context.Request.Query["token"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(token) || !string.Equals(token, settings.CatalogFeedToken, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }
        }
        return Results.Ok();
    }

    private static async Task<IResult> HandleMetaXmlFeed(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settingsSvc = scope.ServiceProvider.GetRequiredService<DeliverySettingsService>();
        var tokenCheck = await ValidateTokenAsync(context, settingsSvc);
        if (tokenCheck is not Microsoft.AspNetCore.Http.HttpResults.Ok) return tokenCheck;

        var baseUrl = "https://www.jasinternational.com.bd/";
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inventoryMap = await db.InventoryItems.AsNoTracking()
            .ToDictionaryAsync(i => i.ProductVariantId, i => i.AvailableQuantity);

        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p!.Brand)
            .Include(v => v.Product).ThenInclude(p => p!.Category)
            .Include(v => v.Product).ThenInclude(p => p!.ProductImages)
            .Include(v => v.Options).ThenInclude(o => o.ProductAttributeOption).ThenInclude(pao => pao!.ProductAttributeDefinition)
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
            var id = (!string.IsNullOrWhiteSpace(v.SKU) ? v.SKU : $"V{v.Id}").Trim().ToUpperInvariant();
            var title = BuildVariantTitle(v, prod);

            var description = !string.IsNullOrWhiteSpace(prod.Description) ? prod.Description.Trim() : title;
            description = Regex.Replace(description, "<.*?>", string.Empty);
            if (description.Length > 4900) description = description[..4900];

            var (label0, label1, label2, label3, label4) = ComputeCustomLabels(v, prod, availableStock);

            var item = new XElement("item",
                new XElement(G + "id", id),
                new XElement(G + "item_group_id", $"P{prod.Id}"),
                new XElement(G + "title", new XCData(title)),
                new XElement(G + "description", new XCData(description)),
                new XElement(G + "link", productUrl),
                new XElement(G + "image_link", prodImage),
                new XElement(G + "brand", brandName),
                new XElement(G + "condition", "new"),
                new XElement(G + "availability", inStock ? "in stock" : "out of stock"),
                new XElement(G + "price", $"{v.SellingPrice:0.00} BDT"),
                new XElement(G + "google_product_category", ResolveGoogleProductCategory(prod.Category?.Slug)),
                new XElement(G + "custom_label_0", label0),
                new XElement(G + "custom_label_1", label1),
                new XElement(G + "custom_label_2", label2),
                new XElement(G + "custom_label_3", label3),
                new XElement(G + "custom_label_4", label4)
            );

            if (!string.IsNullOrWhiteSpace(v.Barcode))
            {
                item.Add(new XElement(G + "gtin", v.Barcode.Trim()));
                item.Add(new XElement(G + "identifier_exists", "true"));
            }
            else
            {
                item.Add(new XElement(G + "identifier_exists", "false"));
            }

            if (v.CompareAtPrice.HasValue && v.CompareAtPrice.Value > v.SellingPrice)
            {
                item.Add(new XElement(G + "sale_price", $"{v.SellingPrice:0.00} BDT"));
                item.SetElementValue(G + "price", $"{v.CompareAtPrice.Value:0.00} BDT");
            }

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
    }

    private static async Task<IResult> HandleMetaCsvFeed(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settingsSvc = scope.ServiceProvider.GetRequiredService<DeliverySettingsService>();
        var tokenCheck = await ValidateTokenAsync(context, settingsSvc);
        if (tokenCheck is not Microsoft.AspNetCore.Http.HttpResults.Ok) return tokenCheck;

        var baseUrl = "https://www.jasinternational.com.bd/";
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inventoryMap = await db.InventoryItems.AsNoTracking()
            .ToDictionaryAsync(i => i.ProductVariantId, i => i.AvailableQuantity);

        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p!.Brand)
            .Include(v => v.Product).ThenInclude(p => p!.Category)
            .Include(v => v.Product).ThenInclude(p => p!.ProductImages)
            .Include(v => v.Options).ThenInclude(o => o.ProductAttributeOption).ThenInclude(pao => pao!.ProductAttributeDefinition)
            .Where(v => v.IsActive && !v.IsDeleted && v.Product != null && !v.Product.IsDeleted)
            .OrderBy(v => v.ProductId)
            .ThenBy(v => v.Id)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("id,title,description,availability,condition,price,link,image_link,brand,item_group_id,google_product_category,gtin,identifier_exists,custom_label_0,custom_label_1,custom_label_2,custom_label_3,custom_label_4");

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
            var id = (!string.IsNullOrWhiteSpace(v.SKU) ? v.SKU : $"V{v.Id}").Trim().ToUpperInvariant();
            var title = BuildVariantTitle(v, prod);

            var description = !string.IsNullOrWhiteSpace(prod.Description) ? prod.Description : title;
            description = Regex.Replace(description, "<.*?>", string.Empty);
            if (description.Length > 2000) description = description[..2000];

            var availability = inStock ? "in stock" : "out of stock";
            var price = $"{v.SellingPrice:0.00} BDT";
            var itemGroupId = $"P{prod.Id}";
            var googleCat = ResolveGoogleProductCategory(prod.Category?.Slug);
            var gtin = !string.IsNullOrWhiteSpace(v.Barcode) ? v.Barcode.Trim() : "";
            var identifierExists = !string.IsNullOrWhiteSpace(v.Barcode) ? "true" : "false";

            var (label0, label1, label2, label3, label4) = ComputeCustomLabels(v, prod, availableStock);

            sb.AppendLine($"{CsvEscape(id)},{CsvEscape(title)},{CsvEscape(description)},{availability},new,{price},{CsvEscape(productUrl)},{CsvEscape(prodImage)},{CsvEscape(brandName)},{CsvEscape(itemGroupId)},{CsvEscape(googleCat)},{CsvEscape(gtin)},{identifierExists},{CsvEscape(label0)},{CsvEscape(label1)},{CsvEscape(label2)},{CsvEscape(label3)},{CsvEscape(label4)}");
        }

        return Results.Content(sb.ToString(), "text/csv; charset=utf-8", Encoding.UTF8);
    }

    private static async Task<IResult> HandleGoogleMerchantXmlFeed(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settingsSvc = scope.ServiceProvider.GetRequiredService<DeliverySettingsService>();
        var tokenCheck = await ValidateTokenAsync(context, settingsSvc);
        if (tokenCheck is not Microsoft.AspNetCore.Http.HttpResults.Ok) return tokenCheck;

        var baseUrl = "https://www.jasinternational.com.bd/";
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inventoryMap = await db.InventoryItems.AsNoTracking()
            .ToDictionaryAsync(i => i.ProductVariantId, i => i.AvailableQuantity);

        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p!.Brand)
            .Include(v => v.Product).ThenInclude(p => p!.Category)
            .Include(v => v.Product).ThenInclude(p => p!.ProductImages)
            .Include(v => v.Options).ThenInclude(o => o.ProductAttributeOption).ThenInclude(pao => pao!.ProductAttributeDefinition)
            .Where(v => v.IsActive && !v.IsDeleted && v.Product != null && !v.Product.IsDeleted)
            .OrderBy(v => v.ProductId)
            .ThenBy(v => v.Id)
            .ToListAsync();

        var channel = new XElement("channel",
            new XElement("title", "JAS International · Google Merchant Center Feed"),
            new XElement("link", baseUrl),
            new XElement("description", "Google Shopping & Merchant Center Product Feed for Bangladesh")
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
            var id = (!string.IsNullOrWhiteSpace(v.SKU) ? v.SKU : $"V{v.Id}").Trim().ToUpperInvariant();
            var title = BuildVariantTitle(v, prod);

            var description = !string.IsNullOrWhiteSpace(prod.Description) ? prod.Description.Trim() : title;
            description = Regex.Replace(description, "<.*?>", string.Empty);
            if (description.Length > 4900) description = description[..4900];

            var (label0, label1, label2, label3, label4) = ComputeCustomLabels(v, prod, availableStock);

            var item = new XElement("item",
                new XElement(G + "id", id),
                new XElement(G + "item_group_id", $"P{prod.Id}"),
                new XElement(G + "title", new XCData(title)),
                new XElement(G + "description", new XCData(description)),
                new XElement(G + "link", productUrl),
                new XElement(G + "image_link", prodImage),
                new XElement(G + "brand", brandName),
                new XElement(G + "condition", "new"),
                new XElement(G + "availability", inStock ? "in stock" : "out of stock"),
                new XElement(G + "price", $"{v.SellingPrice:0.00} BDT"),
                new XElement(G + "google_product_category", ResolveGoogleProductCategory(prod.Category?.Slug)),
                new XElement(G + "custom_label_0", label0),
                new XElement(G + "custom_label_1", label1),
                new XElement(G + "custom_label_2", label2),
                new XElement(G + "custom_label_3", label3),
                new XElement(G + "custom_label_4", label4),
                // Google Shipping Rules (Khulna & Outside)
                new XElement(G + "shipping",
                    new XElement(G + "country", "BD"),
                    new XElement(G + "service", "Standard Khulna Delivery"),
                    new XElement(G + "price", "80.00 BDT")
                ),
                new XElement(G + "shipping",
                    new XElement(G + "country", "BD"),
                    new XElement(G + "service", "Outside Khulna Delivery"),
                    new XElement(G + "price", "130.00 BDT")
                )
            );

            if (!string.IsNullOrWhiteSpace(v.Barcode))
            {
                item.Add(new XElement(G + "gtin", v.Barcode.Trim()));
                item.Add(new XElement(G + "identifier_exists", "true"));
            }
            else
            {
                item.Add(new XElement(G + "identifier_exists", "false"));
            }

            if (v.CompareAtPrice.HasValue && v.CompareAtPrice.Value > v.SellingPrice)
            {
                item.Add(new XElement(G + "sale_price", $"{v.SellingPrice:0.00} BDT"));
                item.SetElementValue(G + "price", $"{v.CompareAtPrice.Value:0.00} BDT");
            }

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
    }

    private static (string Label0, string Label1, string Label2, string Label3, string Label4) ComputeCustomLabels(
        Persistence.Entities.ProductVariant variant,
        Persistence.Entities.Product product,
        int availableStock)
    {
        // Custom Label 0: Margin Tier
        string label0;
        if (variant.CostPrice > 0 && variant.SellingPrice > 0)
        {
            var marginPercent = (variant.SellingPrice - variant.CostPrice) / variant.SellingPrice * 100m;
            label0 = marginPercent >= 30m ? "High Margin" : (marginPercent >= 15m ? "Standard Margin" : "Low Margin");
        }
        else
        {
            label0 = "Standard Margin";
        }

        // Custom Label 1: Price Tier
        var label1 = variant.SellingPrice switch
        {
            < 1000m => "Under 1000 BDT",
            < 5000m => "1000-5000 BDT",
            < 15000m => "5000-15000 BDT",
            _ => "Above 15000 BDT"
        };

        // Custom Label 2: Best Seller / Featured
        var label2 = product.IsFeatured ? "BestSeller" : "Standard";

        // Custom Label 3: New Arrival
        var label3 = product.IsNewArrival ? "NewArrival" : "Regular";

        // Custom Label 4: Real Stock Status
        var label4 = availableStock switch
        {
            > 5 => "InStock",
            > 0 => "LowStock",
            _ => "OutOfStock"
        };

        return (label0, label1, label2, label3, label4);
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        var escaped = value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
        return $"\"{escaped}\"";
    }

    public static string ResolveGoogleProductCategory(string? categorySlug) => (categorySlug?.ToLowerInvariant().Trim()) switch
    {
        "true-wireless-earbuds" or "tws" or "neckbands" or "neckband" or "headphones" or "audio-earbuds" or "audio"
            => "Electronics > Audio > Audio Components > Headphones & Headsets",
        "speakers" or "portable-speakers"
            => "Electronics > Audio > Audio Components > Speakers",
        "microphones" or "microphone"
            => "Electronics > Audio > Audio Components > Microphones",
        "smart-watches" or "smartwatches" or "smart-wearables"
            => "Electronics > Wearable Technology > Smartwatches",
        "fitness-bands"
            => "Electronics > Wearable Technology > Activity Trackers",
        "watch-accessories"
            => "Electronics > Electronics Accessories > Watch Accessories",
        "power-charging" or "power-banks" or "wall-chargers" or "wireless-chargers" or "chargers-power-banks"
            => "Electronics > Electronics Accessories > Power > Power Adapters & Chargers",
        "cables" or "cables-adapters"
            => "Electronics > Electronics Accessories > Cables",
        "converters-hubs"
            => "Electronics > Electronics Accessories > Computer Components > USB & FireWire Hubs",
        "tripods-lighting" or "creator-gear" or "creator-essentials" or "tripods-stands"
            => "Cameras & Optics > Camera & Optic Accessories > Tripods & Monopods",
        "cases-protection" or "screen-protectors" or "protective-cases" or "coolers-stands" or "phone-coolers" or "phone-stands" or "ipad-tablet-accessories"
            => "Electronics > Electronics Accessories",
        _ => "Electronics"
    };

    public static string BuildVariantTitle(Persistence.Entities.ProductVariant v, Persistence.Entities.Product prod)
    {
        var optionNames = string.Join(" / ", v.Options
            .Where(o => o.ProductAttributeOption != null && !string.IsNullOrWhiteSpace(o.ProductAttributeOption.Value))
            .OrderBy(o => o.ProductAttributeOption!.ProductAttributeDefinition?.SortOrder ?? 0)
            .ThenBy(o => o.ProductAttributeDefinitionId)
            .Select(o => o.ProductAttributeOption!.Value.Trim())
            .Where(val => !string.IsNullOrWhiteSpace(val)));

        if (!string.IsNullOrWhiteSpace(optionNames))
        {
            return $"{prod.Name} - {optionNames}";
        }

        // Avoid odd numeric suffixes like "2-3" or "~guid-1"
        if (!string.IsNullOrWhiteSpace(v.OptionSignature) &&
            !v.OptionSignature.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
            !Regex.IsMatch(v.OptionSignature.Trim(), @"^~?[0-9a-fA-F-]+$"))
        {
            return $"{prod.Name} - {v.OptionSignature.Trim()}";
        }

        return prod.Name;
    }
}
