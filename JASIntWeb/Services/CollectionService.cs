using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

public sealed record CollectionMetaInfo(int Id, string Name, string Slug, bool IsActive);

public sealed class CollectionService(
    IDbContextFactory<AppDbContext> dbFactory,
    HybridCache cache,
    IOutputCacheStore? outputCache = null,
    IServiceProvider? services = null)
{
    public const string TagCollections = "collections";

    private static readonly HybridCacheEntryOptions CollectionOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<CollectionMetaInfo?> GetCollectionMetaAsync(int collectionId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"collection:meta:{collectionId}",
            async token =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(token);
                return await db.Collections.AsNoTracking()
                    .Where(c => c.Id == collectionId && !c.IsDeleted)
                    .Select(c => new CollectionMetaInfo(c.Id, c.Name, c.Slug, c.IsActive))
                    .FirstOrDefaultAsync(token);
            },
            options: CollectionOptions,
            tags: [CatalogService.TagCatalog, TagCollections],
            cancellationToken: ct);
    }

    public async Task<List<int>> GetCollectionProductIdsAsync(int collectionId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"collection:ids:{collectionId}",
            async token =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(token);
                var collection = await db.Collections.AsNoTracking()
                    .Where(c => c.Id == collectionId && !c.IsDeleted && c.IsActive)
                    .Select(c => new
                    {
                        c.Id,
                        c.Type,
                        c.SortMode,
                        c.RulesJson
                    })
                    .FirstOrDefaultAsync(token);

                if (collection is null)
                {
                    return [];
                }

                if (collection.Type == CollectionType.Manual)
                {
                    return await db.CollectionProducts.AsNoTracking()
                        .Where(cp => cp.CollectionId == collectionId
                            && !cp.Product!.IsDeleted
                            && !cp.Product.Category!.IsDeleted
                            && (cp.Product.Category.ParentCategoryId == null || !cp.Product.Category.ParentCategory!.IsDeleted)
                            && cp.Product.ProductVariants.Any(v => v.IsActive && !v.IsDeleted))
                        .OrderBy(cp => cp.SortOrder)
                        .ThenBy(cp => cp.ProductId)
                        .Select(cp => cp.ProductId)
                        .ToListAsync(token);
                }

                var rules = CollectionRuleEvaluator.DeserializeRules(collection.RulesJson);
                if (!CollectionRuleEvaluator.HasAnyRules(rules))
                {
                    return [];
                }

                var candidates = await LoadRuleCandidatesAsync(db, token);
                var ctx = await BuildRuleContextAsync(db, token);
                return CollectionRuleEvaluator.EvaluateAndSort(candidates, rules, collection.SortMode, ctx)
                    .Select(p => p.Id)
                    .ToList();
            },
            options: CollectionOptions,
            tags: [CatalogService.TagCatalog, TagCollections],
            cancellationToken: ct);
    }

    public async Task<List<ProductSummaryDto>> GetCollectionProductsAsync(
        int collectionId,
        int? limit = null,
        CancellationToken ct = default)
    {
        var ids = await GetCollectionProductIdsAsync(collectionId, ct);
        if (ids.Count == 0)
        {
            return [];
        }

        var targetIds = limit.HasValue && limit.Value > 0 ? ids.Take(limit.Value).ToList() : ids;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var items = await LoadOrderedProductSummariesAsync(db, targetIds, ct);
        var pricing = services?.GetService<CampaignPricingService>();
        return pricing is not null && items.Count > 0
            ? await pricing.ApplyCampaignPricingAsync(items, ct)
            : items;
    }

    public async Task<(int TotalMatches, List<ProductSummaryDto> PreviewItems)> PreviewRuleMatchesAsync(
        CollectionRuleGroup rules,
        CollectionSortOrder sortMode,
        int take = 24,
        CancellationToken ct = default)
    {
        if (!CollectionRuleEvaluator.HasAnyRules(rules))
        {
            return (0, []);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var candidates = await LoadRuleCandidatesAsync(db, ct);
        var ctx = await BuildRuleContextAsync(db, ct);
        var matched = CollectionRuleEvaluator.EvaluateAndSort(candidates, rules, sortMode, ctx);
        var previewIds = matched.Take(Math.Clamp(take, 1, 60)).Select(c => c.Id).ToList();
        var items = previewIds.Count > 0
            ? await LoadOrderedProductSummariesAsync(db, previewIds, ct)
            : [];

        var pricing = services?.GetService<CampaignPricingService>();
        if (pricing is not null && items.Count > 0)
        {
            items = await pricing.ApplyCampaignPricingAsync(items, ct);
        }

        return (matched.Count, items);
    }

    public async Task<string?> GetPublishedPageSlugForCollectionAsync(int collectionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CollectionPages.AsNoTracking()
            .Where(p => p.CollectionId == collectionId && p.IsPublished)
            .OrderBy(p => p.Id)
            .Select(p => p.Slug)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CollectionPageDetailsDto?> GetPublishedCollectionPageAsync(
        string slug,
        string? query = null,
        string? brandSlug = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool inStockOnly = false,
        string? sort = null,
        int page = 1,
        int pageSize = 18,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var cleanSlug = slug.Trim().ToLowerInvariant();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var pageEntity = await db.CollectionPages.AsNoTracking()
            .Include(p => p.Collection)
            .FirstOrDefaultAsync(p => p.Slug == cleanSlug && p.IsPublished && p.Collection != null && !p.Collection.IsDeleted && p.Collection.IsActive, ct);

        if (pageEntity is null)
        {
            return null;
        }

        var orderedIds = await GetCollectionProductIdsAsync(pageEntity.CollectionId, ct);
        var allProducts = orderedIds.Count > 0
            ? await LoadOrderedProductSummariesAsync(db, orderedIds, ct)
            : [];
        var pricing = services?.GetService<CampaignPricingService>();
        if (pricing is not null && allProducts.Count > 0)
        {
            allProducts = await pricing.ApplyCampaignPricingAsync(allProducts, ct);
        }

        var brandSlugsInCollection = allProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.BrandSlug))
            .Select(p => p.BrandSlug!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableBrands = brandSlugsInCollection.Count > 0
            ? await db.Brands.AsNoTracking()
                .Where(b => !b.IsDeleted && brandSlugsInCollection.Contains(b.Slug))
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.Name)
                .Select(b => new BrandLinkDto(b.Id, b.Name, b.Slug, b.LogoUrl, b.IsFeatured, b.SortOrder))
                .ToListAsync(ct)
            : [];

        IEnumerable<ProductSummaryDto> filtered = allProducts;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            filtered = filtered.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.BrandName != null && p.BrandName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(brandSlug))
        {
            filtered = filtered.Where(p => string.Equals(p.BrandSlug, brandSlug.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (minPrice.HasValue)
        {
            filtered = filtered.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            filtered = filtered.Where(p => p.Price <= maxPrice.Value);
        }

        if (inStockOnly)
        {
            filtered = filtered.Where(p => p.DefaultVariantStock > 0);
        }

        filtered = sort switch
        {
            "price-low" => filtered.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price-high" => filtered.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "name" => filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id),
            "newest" => filtered.OrderByDescending(p => p.IsNewArrival).ThenByDescending(p => p.Id),
            _ => filtered // Preserve collection's curated / rule sort order
        };

        var filteredList = filtered.ToList();
        var total = filteredList.Count;
        var safePage = Math.Clamp(page, 1, 10000);
        var pagedItems = filteredList
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new CollectionPageDetailsDto(
            pageEntity.Id,
            pageEntity.CollectionId,
            pageEntity.Collection!.Name,
            pageEntity.Title,
            pageEntity.Slug,
            pageEntity.Eyebrow,
            pageEntity.Description,
            pageEntity.BannerImageUrl,
            pageEntity.MobileBannerImageUrl,
            pageEntity.ThemeColor,
            pageEntity.ShowFilters,
            pageEntity.MetaTitle,
            pageEntity.MetaDescription,
            pageEntity.OgImageUrl,
            pagedItems,
            total,
            availableBrands
        );
    }

    public async Task InvalidateCacheAsync(int? collectionId = null, CancellationToken ct = default)
    {
        services?.GetService<CampaignPricingService>()?.InvalidateCache();
        if (collectionId.HasValue)
        {
            await cache.RemoveAsync($"collection:ids:{collectionId.Value}", ct);
            await cache.RemoveAsync($"collection:meta:{collectionId.Value}", ct);
        }

        await cache.RemoveByTagAsync(TagCollections, ct);
        await cache.RemoveByTagAsync(CatalogService.TagSections, ct);

        if (outputCache != null)
        {
            try
            {
                await outputCache.EvictByTagAsync("homepage", ct);
            }
            catch
            {
                // Best-effort output cache eviction
            }
        }
    }

    private static async Task<List<CollectionProductCandidate>> LoadRuleCandidatesAsync(AppDbContext db, CancellationToken ct)
    {
        var raw = await db.Products.AsNoTracking()
            .Where(p => !p.IsDeleted
                && !p.Category!.IsDeleted
                && (p.Category.ParentCategoryId == null || !p.Category.ParentCategory!.IsDeleted)
                && p.ProductVariants.Any(v => v.IsActive && !v.IsDeleted))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.CategoryId,
                p.BrandId,
                BrandSlug = p.Brand != null ? p.Brand.Slug : null,
                TagIds = p.ProductTags.Where(pt => !pt.Tag.IsDeleted).Select(pt => pt.TagId).ToList(),
                DefaultVariant = p.ProductVariants
                    .Where(v => v.IsActive && !v.IsDeleted)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenBy(v => v.Id)
                    .Select(v => new { v.Id, v.SellingPrice, v.CompareAtPrice })
                    .FirstOrDefault(),
                p.IsFeatured,
                p.IsNewArrival,
                p.Rating,
                p.CreatedAt
            })
            .ToListAsync(ct);

        var variantIds = raw.Where(r => r.DefaultVariant != null).Select(r => r.DefaultVariant!.Id).ToList();
        var stockMap = variantIds.Count > 0
            ? await db.InventoryItems.AsNoTracking()
                .Where(i => variantIds.Contains(i.ProductVariantId))
                .ToDictionaryAsync(i => i.ProductVariantId, i => i.QuantityOnHand - i.QuantityReserved, ct)
            : new Dictionary<int, int>();

        var result = new List<CollectionProductCandidate>(raw.Count);
        foreach (var p in raw)
        {
            if (p.DefaultVariant is null) continue;
            var stock = stockMap.TryGetValue(p.DefaultVariant.Id, out var qty) ? qty : 0;
            result.Add(new CollectionProductCandidate(
                p.Id,
                p.Name,
                p.Slug,
                p.CategoryId,
                p.BrandId,
                p.BrandSlug,
                p.TagIds,
                p.DefaultVariant.SellingPrice,
                p.DefaultVariant.CompareAtPrice,
                stock,
                p.IsFeatured,
                p.IsNewArrival,
                p.Rating,
                p.CreatedAt));
        }

        return result;
    }

    private static async Task<CollectionRuleContext> BuildRuleContextAsync(AppDbContext db, CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .Select(c => new { c.Id, c.Slug, c.ParentCategoryId })
            .ToListAsync(ct);

        var tags = await db.Tags.AsNoTracking()
            .Where(t => !t.IsDeleted)
            .Select(t => new { t.Id, t.Slug })
            .ToListAsync(ct);

        var brands = await db.Brands.AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Select(b => new { b.Id, b.Slug })
            .ToListAsync(ct);

        var childrenLookup = categories
            .Where(c => c.ParentCategoryId.HasValue)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var subtrees = new Dictionary<int, HashSet<int>>();
        foreach (var cat in categories)
        {
            var set = new HashSet<int> { cat.Id };
            var queue = new Queue<int>();
            queue.Enqueue(cat.Id);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (childrenLookup.TryGetValue(cur, out var kids))
                {
                    foreach (var kid in kids)
                    {
                        if (set.Add(kid))
                        {
                            queue.Enqueue(kid);
                        }
                    }
                }
            }
            subtrees[cat.Id] = set;
        }

        var catSlugs = categories.ToDictionary(c => c.Slug.Trim().ToLowerInvariant(), c => c.Id, StringComparer.OrdinalIgnoreCase);
        var tagSlugs = tags.ToDictionary(t => t.Slug.Trim().ToLowerInvariant(), t => t.Id, StringComparer.OrdinalIgnoreCase);
        var brandSlugs = brands.ToDictionary(b => b.Slug.Trim().ToLowerInvariant(), b => b.Id, StringComparer.OrdinalIgnoreCase);

        return new CollectionRuleContext(subtrees, catSlugs, tagSlugs, brandSlugs);
    }

    public static async Task<List<ProductSummaryDto>> LoadOrderedProductSummariesAsync(
        AppDbContext db,
        IReadOnlyList<int> orderedIds,
        CancellationToken ct = default)
    {
        if (orderedIds.Count == 0) return [];

        var rawProducts = await db.Products.AsNoTracking()
            .Where(p => orderedIds.Contains(p.Id)
                && !p.IsDeleted
                && p.ProductVariants.Any(v => v.IsActive && !v.IsDeleted))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                Price = p.ProductVariants
                    .Where(v => v.IsActive && !v.IsDeleted)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenBy(v => v.Id)
                    .Select(v => v.SellingPrice)
                    .FirstOrDefault(),
                CategoryName = p.Category != null ? p.Category.Name : "",
                p.MainImageUrl,
                DefaultVariantId = p.ProductVariants
                    .Where(v => v.IsActive && !v.IsDeleted)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenBy(v => v.Id)
                    .Select(v => (int?)v.Id)
                    .FirstOrDefault(),
                AvailableStock = db.InventoryItems
                    .Where(i => i.ProductVariant!.ProductId == p.Id && i.ProductVariant.IsDefault && i.ProductVariant.IsActive && !i.ProductVariant.IsDeleted)
                    .Select(i => i.QuantityOnHand - i.QuantityReserved)
                    .FirstOrDefault(),
                BrandName = p.Brand != null ? p.Brand.Name : null,
                BrandSlug = p.Brand != null ? p.Brand.Slug : null,
                CompareAtPrice = p.ProductVariants
                    .Where(v => v.IsActive && !v.IsDeleted)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenBy(v => v.Id)
                    .Select(v => v.CompareAtPrice)
                    .FirstOrDefault(),
                Rating = db.ProductReviews.Where(r => r.ProductId == p.Id && r.IsApproved).Average(r => (decimal?)r.Rating) ?? p.Rating,
                ReviewCount = db.ProductReviews.Count(r => r.ProductId == p.Id && r.IsApproved),
                p.IsNewArrival,
                p.IsFeatured,
                HasVariants = p.Attributes.Any(),
                TopTag = p.ProductTags
                    .Where(pt => !pt.Tag.IsDeleted && !pt.Tag.IsHidden)
                    .OrderBy(pt => pt.Tag.SortOrder)
                    .Select(pt => new TagDto(pt.Tag.Id, pt.Tag.Name, pt.Tag.Slug, pt.Tag.IsHidden, pt.Tag.BadgeColor, pt.Tag.SortOrder, 0))
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var map = rawProducts.ToDictionary(p => p.Id);
        var ordered = new List<ProductSummaryDto>(orderedIds.Count);
        foreach (var id in orderedIds)
        {
            if (map.TryGetValue(id, out var p))
            {
                ordered.Add(new ProductSummaryDto(
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Price,
                    p.CategoryName,
                    p.MainImageUrl,
                    p.DefaultVariantId,
                    p.AvailableStock,
                    p.BrandName,
                    p.BrandSlug,
                    p.CompareAtPrice,
                    p.Rating > 0 ? p.Rating : 5.0m,
                    p.ReviewCount > 0 ? p.ReviewCount : 12,
                    p.IsNewArrival,
                    p.IsFeatured,
                    p.HasVariants)
                {
                    TopTag = p.TopTag
                });
            }
        }

        return ordered;
    }
}
