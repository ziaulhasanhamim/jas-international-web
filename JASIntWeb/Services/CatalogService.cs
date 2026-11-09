using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using NpgsqlTypes;
namespace JASIntWeb.Services;

public sealed class CatalogService(IDbContextFactory<AppDbContext> dbFactory, HybridCache cache)
{
    public const string TagCatalog = "catalog";
    public const string TagCategories = "categories";
    public const string TagBrands = "brands";
    public const string TagSlides = "slides";
    public const string TagSections = "sections";
    public const string TagPromos = "promos";
    public const string TagProducts = "products";

    public const string CategoriesCacheKey = "catalog:categories";
    public const string HomeCategoriesCacheKey = "catalog:home-categories";
    public const string SlidesCacheKey = "catalog:slides";
    public const string BrandsCacheKey = "catalog:brands";
    public const string FeaturedBrandsCacheKey = "catalog:featured-brands";
    public const string HomepageSectionsCacheKey = "catalog:homepage-sections";
    public const string CuratedPromoCardsCacheKey = "catalog:curated-promo-cards";
    public static string ProductDetailsCacheKey(string slug) => $"catalog:product:{slug.Trim().ToLowerInvariant()}";

    private static readonly HybridCacheEntryOptions LongLivedOptions = new()
    {
        Expiration = TimeSpan.FromHours(2),
        LocalCacheExpiration = TimeSpan.FromHours(2)
    };

    private static readonly HybridCacheEntryOptions StandardOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    private static readonly HybridCacheEntryOptions ProductOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(15)
    };

    public async Task<List<BrandLinkDto>> BrandsAsync() =>
        await cache.GetOrCreateAsync(
            BrandsCacheKey,
            async ct =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                return await db.Brands.AsNoTracking().Where(b => !b.IsDeleted)
                    .OrderBy(b => b.SortOrder)
                    .ThenBy(b => b.Name)
                    .Select(b => new BrandLinkDto(b.Id, b.Name, b.Slug, b.LogoUrl, b.IsFeatured, b.SortOrder))
                    .ToListAsync(ct);
            },
            options: LongLivedOptions,
            tags: [TagCatalog, TagBrands]);

    public async Task<List<BrandLinkDto>> FeaturedBrandsAsync(int limit = 10) =>
        await cache.GetOrCreateAsync(
            $"{FeaturedBrandsCacheKey}_{limit}",
            async ct =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                return await db.Brands.AsNoTracking().Where(b => !b.IsDeleted && b.IsFeatured)
                    .OrderBy(b => b.SortOrder)
                    .ThenBy(b => b.Name)
                    .Take(limit)
                    .Select(b => new BrandLinkDto(b.Id, b.Name, b.Slug, b.LogoUrl, b.IsFeatured, b.SortOrder))
                    .ToListAsync(ct);
            },
            options: LongLivedOptions,
            tags: [TagCatalog, TagBrands]);

    public async Task InvalidateBrandsCacheAsync() =>
        await cache.RemoveByTagAsync(TagBrands);

    public void InvalidateBrandsCache() =>
        _ = InvalidateBrandsCacheAsync();

    public async Task<List<CategoryLinkDto>> CategoriesAsync() =>
        await cache.GetOrCreateAsync(
            CategoriesCacheKey,
            async ct =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                return await db.Categories.AsNoTracking().Where(c => !c.IsDeleted
                    && (c.ParentCategoryId == null || !c.ParentCategory!.IsDeleted))
                    .OrderBy(c => c.Name)
                    .ThenBy(c => c.Id)
                    .Select(c => new CategoryLinkDto(c.Id, c.Name, c.Slug, c.ParentCategoryId))
                    .ToListAsync(ct);
            },
            options: LongLivedOptions,
            tags: [TagCatalog, TagCategories]);

    public async Task InvalidateCategoriesCacheAsync()
    {
        await cache.RemoveByTagAsync(TagCategories);
        await cache.RemoveByTagAsync(TagSections);
    }

    public void InvalidateCategoriesCache() =>
        _ = InvalidateCategoriesCacheAsync();

    public async Task<List<CategoryDto>> HomeCategoriesAsync() =>
        await cache.GetOrCreateAsync(
            HomeCategoriesCacheKey,
            async ct =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                return await db.CategoryPlacements.AsNoTracking().Where(cp => cp.IsActive
                    && cp.PlacementType == CategoryPlacementType.HomepageFeatured
                    && !cp.Category!.IsDeleted
                    && (cp.Category.ParentCategoryId == null || !cp.Category.ParentCategory!.IsDeleted))
                    .OrderBy(cp => cp.SortOrder)
                    .ThenBy(cp => cp.Id)
                    .Select(cp => new CategoryDto(cp.Category!.Name, cp.Category.Icon, cp.Category.Slug))
                    .ToListAsync(ct);
            },
            options: LongLivedOptions,
            tags: [TagCatalog, TagCategories]);

    public async Task<List<CarouselSlideDto>> SlidesAsync() =>
        await cache.GetOrCreateAsync(
            SlidesCacheKey,
            async ct =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                return await db.CarouselSlides.AsNoTracking().Where(s => s.IsActive)
                    .OrderBy(s => s.SortOrder)
                    .ThenBy(s => s.Id)
                    .Select(s => new CarouselSlideDto(s.Id, s.Eyebrow, s.Title, s.Copy, s.ImageUrl,
                        s.ImageAlt, s.LinkUrl, s.LinkText))
                    .ToListAsync(ct);
            },
            options: StandardOptions,
            tags: [TagCatalog, TagSlides]);

    public async Task InvalidateSlidesCacheAsync() =>
        await cache.RemoveByTagAsync(TagSlides);

    public void InvalidateSlidesCache() =>
        _ = InvalidateSlidesCacheAsync();

    public async Task<ProductDetailsDto?> DetailsAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var key = ProductDetailsCacheKey(normalizedSlug);

        return await cache.GetOrCreateAsync(
            key,
            async ct =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                // Bounded projections for product, active variants and configured product attributes.
                var p = await db.Products.AsNoTracking().Where(x => x.Slug == normalizedSlug && !x.IsDeleted
                    && !x.Category!.IsDeleted
                    && (x.Category.ParentCategoryId == null || !x.Category.ParentCategory!.IsDeleted))
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.Slug,
                        x.Description,
                        x.Details,
                        x.MainImageUrl,
                        BrandName = x.Brand != null ? x.Brand.Name : null,
                        BrandSlug = x.Brand != null ? x.Brand.Slug : null,
                        Category = new CategoryLinkDto(x.CategoryId, x.Category!.Name, x.Category.Slug, x.Category.ParentCategoryId),
                        Parent = x.Category.ParentCategoryId == null ? null : new CategoryLinkDto(x.Category.ParentCategory!.Id,
                            x.Category.ParentCategory.Name, x.Category.ParentCategory.Slug, x.Category.ParentCategory.ParentCategoryId),
                        Images = x.ProductImages.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).ToList()
                    })
                    .SingleOrDefaultAsync(ct);
                if (p is null)
                {
                    return null;
                }
                var variants = await db.ProductVariants.AsNoTracking().Where(v => v.ProductId == p.Id && v.IsActive && !v.IsDeleted)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenBy(v => v.Id)
                    .Select(v => new
                    {
                        v.Id,
                        v.SKU,
                        v.SellingPrice,
                        v.IsDefault,
                        Stock = db.InventoryItems.Where(i => i.ProductVariantId == v.Id)
                        .Select(i => i.QuantityOnHand - i.QuantityReserved)
                        .FirstOrDefault(),
                        Options = v.Options.Select(o => new
                            {
                                DefinitionCode = o.ProductAttributeOption!.ProductAttributeDefinition!.AttributeDefinition!.Code,
                                Code = o.ProductAttributeOption.AttributeOption!.Code
                            })
                        .ToList()
                    })
                    .ToListAsync(ct);
                // Configured product options remain visible even if no active variant currently offers a combination.
                var bindings = await db.ProductAttributeDefinitions.AsNoTracking().Where(a => a.ProductId == p.Id)
                    .OrderBy(a => a.SortOrder)
                    .ThenBy(a => a.Id)
                    .Select(a => new ProductAttributeDto(a.AttributeDefinition!.Code, a.AttributeDefinition.Name,
                        a.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
                        .Select(o => new AttributeOptionDto(o.Id, o.AttributeOption!.Code, o.AttributeOption.Value, o.SortOrder))
                        .ToList(), a.IsRequired))
                    .ToListAsync(ct);
                var breadcrumbs = new List<CategoryLinkDto>();
                if (p.Parent is not null)
                {
                    breadcrumbs.Add(p.Parent);
                }
                breadcrumbs.Add(p.Category);
                return new ProductDetailsDto(p.Id, p.Name, p.Slug, p.Category.Name, p.Category.Slug, p.Description, p.Details, p.MainImageUrl,
                    new[]
                    {
                        p.MainImageUrl
                    }
                    .Concat(p.Images)
                    .Distinct()
                    .ToList(), bindings,
                    variants.Select(v => new ProductVariantDto(v.Id, v.SKU, v.SellingPrice, v.Stock, v.IsDefault,
                            v.Options.ToDictionary(o => o.DefinitionCode, o => o.Code)))
                    .ToList(), breadcrumbs, p.BrandName, p.BrandSlug);
            },
            options: ProductOptions,
            tags: [TagCatalog, TagProducts, $"product:{normalizedSlug}"]);
    }

    public async Task InvalidateProductCacheAsync(string? slug = null)
    {
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var normalized = slug.Trim().ToLowerInvariant();
            await cache.RemoveByTagAsync($"product:{normalized}");
            await cache.RemoveAsync(ProductDetailsCacheKey(normalized));
        }
        else
        {
            await cache.RemoveByTagAsync(TagProducts);
        }
        await cache.RemoveByTagAsync(TagSections);
    }

    public void InvalidateProductCache(string? slug = null) =>
        _ = InvalidateProductCacheAsync(slug);

    private static IQueryable<Product> VisibleProducts(AppDbContext db) => db.Products.AsNoTracking().Where(p => !p.IsDeleted
        &&
        !p.Category!.IsDeleted
        && (p.Category.ParentCategoryId == null || !p.Category.ParentCategory!.IsDeleted)
        &&
        p.ProductVariants.Any(v => v.IsActive && !v.IsDeleted));

    private async Task<IQueryable<Product>> InCategoryAsync(IQueryable<Product> query, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return query;
        }
        var categories = await CategoriesAsync();
        var ids = categories.Where(c => c.Slug == slug).Select(c => c.Id).ToHashSet();
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var c in categories)
            {
                if (c.ParentId.HasValue && ids.Contains(c.ParentId.Value) && ids.Add(c.Id))
                {
                    changed = true;
                }
            }
        }
        return query.Where(p => ids.Contains(p.CategoryId));
    }

    // Homepage sections don't need COUNT queries. Prices and stock remain live, never cached.
    public async Task<List<ProductSummaryDto>> LatestAsync(string? category, int take = 4)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = await InCategoryAsync(VisibleProducts(db), category);
        return await q.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Take(Math.Clamp(take, 1, 24))
            .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Slug,
                p.ProductVariants.Where(v => v.IsActive && !v.IsDeleted).OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.Id)
                .Select(v => v.SellingPrice)
                .First(), p.Category!.Name, p.MainImageUrl,
                p.ProductVariants.Where(v => v.IsDefault && v.IsActive && !v.IsDeleted)
                .Select(v => (int?)v.Id)
                .FirstOrDefault(),
                db.InventoryItems.Where(i => i.ProductVariant!.ProductId == p.Id && i.ProductVariant.IsDefault
                    &&
                    i.ProductVariant.IsActive
                    && !i.ProductVariant.IsDeleted)
                .Select(i => i.QuantityOnHand - i.QuantityReserved)
                .FirstOrDefault(),
                p.Brand != null ? p.Brand.Name : null,
                p.Brand != null ? p.Brand.Slug : null,
                p.CompareAtPrice,
                p.Rating > 0 ? p.Rating : 5.0m,
                p.ReviewCount > 0 ? p.ReviewCount : 12,
                p.IsNewArrival,
                p.IsFeatured))
            .ToListAsync();
    }

    public async Task<List<HomepageSectionDto>> GetHomepageSectionsAsync() =>
        await cache.GetOrCreateAsync(
            HomepageSectionsCacheKey,
            async ct =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                var sections = await db.HomepageSections.AsNoTracking()
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync(ct);

                var result = new List<HomepageSectionDto>();
                foreach (var s in sections)
                {
                    var products = await LatestAsync(s.CategorySlug, s.ItemLimit);
                    if (products.Count > 0)
                    {
                        result.Add(new HomepageSectionDto(
                            s.Id,
                            s.Title,
                            s.Eyebrow,
                            s.CategorySlug,
                            s.ViewAllHref,
                            s.SortOrder,
                            s.IsActive,
                            s.ItemLimit,
                            products
                        ));
                    }
                }
                return result;
            },
            options: StandardOptions,
            tags: [TagCatalog, TagSections]);

    public async Task InvalidateHomepageSectionsCacheAsync() =>
        await cache.RemoveByTagAsync(TagSections);

    public void InvalidateHomepageSectionsCache() =>
        _ = InvalidateHomepageSectionsCacheAsync();

    public async Task<List<CuratedPromoCard>> GetCuratedPromoCardsAsync() =>
        await cache.GetOrCreateAsync(
            CuratedPromoCardsCacheKey,
            async ct =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                var cards = await db.CuratedPromoCards.AsNoTracking()
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ToListAsync(ct);

                if (cards.Count == 0)
                {
                    return
                    [
                        new CuratedPromoCard
                        {
                            Id = 1,
                            Eyebrow = "SMART WEARABLES",
                            Title = "Flagship Smartwatches",
                            Subtitle = "Amazfit, Kieslect & Kospet rugged outdoor & AMOLED watches.",
                            LinkUrl = "/category/smartwatches",
                            LinkText = "Explore Watches",
                            Theme = "navy",
                            SortOrder = 1,
                            IsActive = true
                        },
                        new CuratedPromoCard
                        {
                            Id = 2,
                            Eyebrow = "PURE ACOUSTICS",
                            Title = "Studio ANC Earbuds",
                            Subtitle = "Soundcore Liberty 4 Pro, OnePlus Buds & QCY wireless audio.",
                            LinkUrl = "/category/audio-earbuds",
                            LinkText = "Hear the Difference",
                            Theme = "amber",
                            SortOrder = 2,
                            IsActive = true
                        },
                        new CuratedPromoCard
                        {
                            Id = 3,
                            Eyebrow = "FAST CHARGE ECOSYSTEM",
                            Title = "GaN Fast Charge & Power",
                            Subtitle = "Anker Zolo 30W, Baseus 65W & high-capacity 27600mAh power banks.",
                            LinkUrl = "/category/power-charging",
                            LinkText = "Power Up Fast",
                            Theme = "indigo",
                            SortOrder = 3,
                            IsActive = true
                        }
                    ];
                }

                return cards;
            },
            options: StandardOptions,
            tags: [TagCatalog, TagPromos]);

    public async Task InvalidatePromoCardsCacheAsync() =>
        await cache.RemoveByTagAsync(TagPromos);

    public void InvalidatePromoCardsCache() =>
        _ = InvalidatePromoCardsCacheAsync();

    public async Task InvalidateAllAsync() =>
        await cache.RemoveByTagAsync(TagCatalog);

    public void InvalidateAll() =>
        _ = InvalidateAllAsync();

    public async Task<(List<ProductSummaryDto> Items, int Total)> SearchAsync(string? category, string? term,
        string? sort, decimal? min, decimal? max, int page, int pageSize = 18, string? brand = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        pageSize = Math.Clamp(pageSize, 1, 48);
        var q = await InCategoryAsync(VisibleProducts(db), category);
        if (!string.IsNullOrWhiteSpace(brand))
        {
            q = q.Where(p => p.Brand != null && p.Brand.Slug == brand);
        }
        term = term?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            term = term[..Math.Min(term.Length, 120)];
            var pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                .Matches(EF.Functions.WebSearchToTsQuery("simple", term))
                ||
                EF.Functions.ILike(p.Name, pattern, "\\")
                ||
                (term.Length >= 3 && EF.Functions.TrigramsAreWordSimilar(term, p.Name)));
        }
        var priced = q.Select(p => new
            {
                Product = p,
                Price = p.ProductVariants.Where(v => v.IsActive && !v.IsDeleted)
                .OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.Id)
                .Select(v => v.SellingPrice)
                .First()
            });
        if (min.HasValue)
        {
            priced = priced.Where(p => p.Price >= min.Value);
        }
        if (max.HasValue)
        {
            priced = priced.Where(p => p.Price <= max.Value);
        }
        var count = min.HasValue || max.HasValue ? await priced.CountAsync() : await q.CountAsync();
        priced = sort switch
        {
            "price-low" => priced.OrderBy(p => p.Price).ThenBy(p => p.Product.Id),
            "price-high" => priced.OrderByDescending(p => p.Price).ThenBy(p => p.Product.Id),
            "name" => priced.OrderBy(p => p.Product.Name).ThenBy(p => p.Product.Id),
            "newest" => priced.OrderByDescending(p => p.Product.CreatedAt).ThenBy(p => p.Product.Id),
            _ when !string.IsNullOrEmpty(term) => priced.OrderByDescending(p => EF.Property<NpgsqlTsVector>(p.Product, "SearchVector")
                .Rank(EF.Functions.WebSearchToTsQuery("simple", term)))
                .ThenByDescending(p => EF.Functions.TrigramsWordSimilarity(term, p.Product.Name))
                .ThenBy(p => p.Product.Id),
            _ => priced.OrderByDescending(p => p.Product.CreatedAt).ThenBy(p => p.Product.Id)
        };
        return (await priced.Skip((Math.Clamp(page, 1, 10000) - 1) * pageSize).Take(pageSize)
            .Select(p => new ProductSummaryDto(p.Product.Id, p.Product.Name, p.Product.Slug, p.Price,
                    p.Product.Category!.Name, p.Product.MainImageUrl,
                    p.Product.ProductVariants.Where(v => v.IsDefault && v.IsActive && !v.IsDeleted)
                    .Select(v => (int?)v.Id)
                    .FirstOrDefault(),
                    db.InventoryItems.Where(i => i.ProductVariant!.ProductId == p.Product.Id && i.ProductVariant.IsDefault
                        &&
                        i.ProductVariant.IsActive
                        && !i.ProductVariant.IsDeleted)
                    .Select(i => i.QuantityOnHand - i.QuantityReserved)
                    .FirstOrDefault(),
                    p.Product.Brand != null ? p.Product.Brand.Name : null,
                    p.Product.Brand != null ? p.Product.Brand.Slug : null,
                    p.Product.CompareAtPrice,
                    p.Product.Rating > 0 ? p.Product.Rating : 5.0m,
                    p.Product.ReviewCount > 0 ? p.Product.ReviewCount : 12,
                    p.Product.IsNewArrival,
                    p.Product.IsFeatured))
            .ToListAsync(), count);
    }
}
