using System.ComponentModel.DataAnnotations;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;
namespace JASIntWeb.Services;

public sealed class ServerAdminCatalogBackend(AdminSession session, IServiceScopeFactory scopes, IConfiguration configuration) : IAdminCatalogBackend
{
    private string PublicBaseUrl => configuration["PublicBaseUrl"] ?? "https://www.jasinternational.com.bd";
    private AdminEditorData<T> Data<T>(T input, List<CategoryLinkDto>? categories = null, List<BrandLinkDto>? brands = null, int children = 0, int products = 0)
        => new(input, categories ?? [], brands ?? [], children, products, PublicBaseUrl);
    private Task<Result<T>> Execute<T>(Func<AdminService, Task<Result<T>>> action) =>
        session.RunAsync((service, _) => action(service));
    private Task<Result<T>> Execute<T>(Func<AdminService, Task<T>> action) =>
        session.RunAsync(action);
    private Task<Result> Execute(Func<AdminService, Task<Result>> action) =>
        session.RunAsync((service, _) => action(service));
    private Task<Result> Execute(Func<AdminService, Task> action) =>
        session.RunAsync(action);

    public Task<Result<AdminEditorData<CategoryEditForm>>> CategoryAsync(int Id) => Execute<AdminEditorData<CategoryEditForm>>(async service =>
    {
        CategoryEditForm? input = null;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<CategoryLinkDto> parents = [];
        int childCount;

        childCount = Id != 0 ? await db.Categories.CountAsync(c => c.ParentCategoryId == Id && !c.IsDeleted) : 0;
        parents = await db.Categories.AsNoTracking()
            .Where(c => c.Id != Id
            && !c.IsDeleted
            && c.ParentCategoryId == null)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryLinkDto(c.Id, c.Name, c.Slug, c.ParentCategoryId)).ToListAsync();
        if (Id == 0)
        {
            input = new();
            return Data(input!, parents, children: childCount);
        }
        var c = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == Id);
        if (c is null)
        {
            return (Result<AdminEditorData<CategoryEditForm>>)new NotFoundError("Record not found.");
        }
        input = new()
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Icon = c.Icon ?? "",
            ParentId = c.ParentCategoryId,
            Hidden = c.IsDeleted,
            MetaTitle = c.MetaTitle,
            MetaDescription = c.MetaDescription,
            OgImageUrl = c.OgImageUrl,
            IntroText = c.IntroText,
            BottomSeoCopy = c.BottomSeoCopy,
            Featured = await db.CategoryPlacements.AnyAsync(p => p.CategoryId == Id
                && p.IsActive
                && p.PlacementType == CategoryPlacementType.HomepageFeatured)
        };

        return Data(input!, parents, children: childCount);
    });

    public Task<Result<AdminEditorData<BrandEditForm>>> BrandAsync(int Id) => Execute<AdminEditorData<BrandEditForm>>(async service =>
    {
        BrandEditForm? input = null;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var productCount = 0;

        if (Id != 0)
        {
            productCount = await db.Products.CountAsync(p => p.BrandId == Id && !p.IsDeleted);
        }

        if (Id == 0)
        {
            input = new BrandEditForm
            {
                SortOrder = (await db.Brands.MaxAsync(b => (int?)b.SortOrder) ?? 0) + 1
            };
        }
        else
        {
            var b = await db.Brands.SingleOrDefaultAsync(x => x.Id == Id);
            if (b is null)
            {
                return (Result<AdminEditorData<BrandEditForm>>)new NotFoundError("Record not found.");
            }
            input = new BrandEditForm
            {
                Id = b.Id,
                Name = b.Name,
                Slug = b.Slug,
                Description = b.Description,
                LogoUrl = b.LogoUrl,
                IsFeatured = b.IsFeatured,
                SortOrder = b.SortOrder,
                Hidden = b.IsDeleted,
                MetaTitle = b.MetaTitle,
                MetaDescription = b.MetaDescription,
                OgImageUrl = b.OgImageUrl,
                IntroText = b.IntroText,
                BottomSeoCopy = b.BottomSeoCopy
            };
        }

        return Data(input!, products: productCount);
    });

    public Task<Result<AdminEditorData<ProductEditForm>>> ProductAsync(int Id) => Execute<AdminEditorData<ProductEditForm>>(async service =>
    {
        ProductEditForm? input = null;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<CategoryLinkDto> categories;
        List<BrandLinkDto> brands;

        categories = await db.Categories.AsNoTracking()
            .Where(c => !c.IsDeleted
            && (c.ParentCategory == null
                || !c.ParentCategory.IsDeleted))
            .OrderBy(c => c.Name)
            .Select(c => new CategoryLinkDto(c.Id, c.Name, c.Slug, c.ParentCategoryId))
            .ToListAsync();

        brands = await db.Brands.AsNoTracking()
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Name)
            .Select(b => new BrandLinkDto(b.Id, b.Name, b.Slug, b.LogoUrl, b.IsFeatured, b.SortOrder))
            .ToListAsync();

        if (Id == 0)
        {
            input = new()
            {
                Hidden = true,
                InitialStock = 10,
                HasVariants = false
            };
            return Data(input!, categories, brands);
        }
        var p = await db.Products.AsNoTracking()
            .Include(p => p.ProductImages)
            .Include(p => p.ProductVariants)
            .SingleOrDefaultAsync(p => p.Id == Id);
        if (p is null)
        {
            return (Result<AdminEditorData<ProductEditForm>>)new NotFoundError("Record not found.");
        }

        var defVariant = p.ProductVariants.FirstOrDefault(v => v.IsDefault && !v.IsDeleted)
                      ?? p.ProductVariants.FirstOrDefault(v => !v.IsDeleted);
        int currentStock = 0;
        if (defVariant != null)
        {
            var inv = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.ProductVariantId == defVariant.Id);
            currentStock = inv?.QuantityOnHand ?? 0;
        }

        input = new()
        {
            Id = p.Id,
            CatalogRevision = p.CatalogRevision,
            Name = p.Name,
            Slug = p.Slug,
            CategoryId = p.CategoryId,
            BrandId = p.BrandId,
            Description = p.Description ?? "",
            Details = p.Details,
            Image = p.MainImageUrl,
            Images = string.Join("\n", p.ProductImages.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl)),
            Hidden = p.IsDeleted,
            IsFeatured = p.IsFeatured,
            IsNewArrival = p.IsNewArrival,
            CompareAtPrice = p.CompareAtPrice,
            MetaTitle = p.MetaTitle,
            MetaDescription = p.MetaDescription,
            OgImageUrl = p.OgImageUrl,
            Synonyms = p.Synonyms,
            BanglaName = p.BanglaName,
            BanglaDescription = p.BanglaDescription,
            WarrantyInfo = p.WarrantyInfo ?? "1 Year Official Brand Warranty",
            BasePrice = defVariant?.SellingPrice,
            CostPrice = defVariant?.CostPrice,
            InitialStock = currentStock,
            Sku = defVariant?.SKU,
            Barcode = defVariant?.Barcode,
            HasVariants = p.ProductVariants.Count(v => !v.IsDeleted) > 1
        };

        return Data(input!, categories, brands);
    });

    private Task<Result<AdminSaveResult>> Save<T>(T input, Func<AdminService, Task<int>> save, Func<int, string> next)
    {
        return Execute<AdminSaveResult>(async service =>
        {
            List<ValidationResult> errors = [];
            if (!Validator.TryValidateObject(input!, new ValidationContext(input!), errors, true))
            {
                var fields = errors.SelectMany(error => error.MemberNames.Select(name => (name, message: error.ErrorMessage ?? "Invalid value.")))
                    .GroupBy(error => error.name)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.message).ToArray());
                return (Result<AdminSaveResult>)new ValidationError("Review the highlighted fields.", fields);
            }
            var id = await save(service);
            return (Result<AdminSaveResult>)new AdminSaveResult(id, next(id));
        });
    }

    public Task<Result<AdminSaveResult>> SaveCategoryAsync(CategoryEditForm input) => Save(input, service => service.SaveCategoryAsync(input), _ => "/admin/categories");
    public Task<Result<AdminSaveResult>> SaveBrandAsync(BrandEditForm input) => Save(input, service => service.SaveBrandAsync(input), _ => "/admin/brands");
    public Task<Result<AdminSaveResult>> SaveProductAsync(ProductEditForm input) => Task.FromResult<Result<AdminSaveResult>>(new Error("The product editor has been updated. Reload and use Save product.", 410));
    public Task<Result<ContentPreview>> PreviewContentAsync(string source) => Execute<ContentPreview>(_ =>
    {
        if (source is null || source.Length > 100_000) return Task.FromResult<Result<ContentPreview>>(new ValidationError("Use at most 100,000 characters."));
        return Task.FromResult<Result<ContentPreview>>(new ContentPreview(new CatalogContentRenderer().Render(source)));
    });

    public Task<Result> DeleteBrandAsync(int id) => Execute(async service =>
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Brands.AnyAsync(b => b.Id == id)) return (Result)new NotFoundError("Brand not found.");
        if (await db.Products.AnyAsync(p => p.BrandId == id && !p.IsDeleted)) return (Result)new ConflictError("Brands with products cannot be deleted from this editor.");
        await service.DeleteBrandAsync(id);
        return (Result)Success.Ok;
    });

    public Task<Result<AdminEditorData<GuideEditForm>>> GuideAsync(int id) => Execute<AdminEditorData<GuideEditForm>>(async service =>
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var categories = await db.Categories.AsNoTracking()
            .Where(c => !c.IsDeleted && c.ParentCategoryId == null)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryLinkDto(c.Id, c.Name, c.Slug, c.ParentCategoryId))
            .ToListAsync();

        var brands = await db.Brands.AsNoTracking()
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.Name)
            .Select(b => new BrandLinkDto(b.Id, b.Name, b.Slug))
            .ToListAsync();

        if (id == 0)
        {
            return Data(new GuideEditForm(), categories, brands);
        }

        var g = await db.Guides.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id);
        if (g is null)
        {
            return (Result<AdminEditorData<GuideEditForm>>)new NotFoundError("Guide not found.");
        }

        var input = new GuideEditForm
        {
            Id = g.Id,
            Title = g.Title,
            Slug = g.Slug,
            Summary = g.Summary,
            ContentHtml = g.ContentHtml,
            BanglaTitle = g.BanglaTitle,
            BanglaSummary = g.BanglaSummary,
            BanglaContentHtml = g.BanglaContentHtml,
            Author = g.Author,
            FeaturedImageUrl = g.FeaturedImageUrl,
            MetaTitle = g.MetaTitle,
            MetaDescription = g.MetaDescription,
            IsPublished = g.IsPublished,
            PublishedAt = g.PublishedAt
        };

        return Data(input, categories, brands);
    });

    public Task<Result<AdminSaveResult>> SaveGuideAsync(GuideEditForm input) => Execute<AdminSaveResult>(async service =>
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string cleanSlug = SlugAndSkuGenerator.GenerateSlug(string.IsNullOrWhiteSpace(input.Slug) ? input.Title : input.Slug);

        var slugExists = await db.Guides.AnyAsync(g => g.Slug == cleanSlug && g.Id != input.Id && !g.IsDeleted);
        if (slugExists)
        {
            return (Result<AdminSaveResult>)new ConflictError("A guide with this URL slug already exists.");
        }

        Guide guide;
        if (input.Id == 0)
        {
            guide = new Guide
            {
                Title = input.Title.Trim(),
                Slug = cleanSlug,
                Summary = input.Summary.Trim(),
                ContentHtml = input.ContentHtml ?? "",
                BanglaTitle = string.IsNullOrWhiteSpace(input.BanglaTitle) ? null : input.BanglaTitle.Trim(),
                BanglaSummary = string.IsNullOrWhiteSpace(input.BanglaSummary) ? null : input.BanglaSummary.Trim(),
                BanglaContentHtml = string.IsNullOrWhiteSpace(input.BanglaContentHtml) ? null : input.BanglaContentHtml,
                Author = string.IsNullOrWhiteSpace(input.Author) ? "JAS Editorial Team" : input.Author.Trim(),
                FeaturedImageUrl = string.IsNullOrWhiteSpace(input.FeaturedImageUrl) ? null : input.FeaturedImageUrl.Trim(),
                MetaTitle = string.IsNullOrWhiteSpace(input.MetaTitle) ? null : input.MetaTitle.Trim(),
                MetaDescription = string.IsNullOrWhiteSpace(input.MetaDescription) ? null : input.MetaDescription.Trim(),
                IsPublished = input.IsPublished,
                PublishedAt = input.PublishedAt
            };
            db.Guides.Add(guide);
        }
        else
        {
            var existing = await db.Guides.SingleOrDefaultAsync(g => g.Id == input.Id);
            if (existing is null)
            {
                return (Result<AdminSaveResult>)new NotFoundError("Guide not found.");
            }

            existing.Title = input.Title.Trim();
            existing.Slug = cleanSlug;
            existing.Summary = input.Summary.Trim();
            existing.ContentHtml = input.ContentHtml ?? "";
            existing.BanglaTitle = string.IsNullOrWhiteSpace(input.BanglaTitle) ? null : input.BanglaTitle.Trim();
            existing.BanglaSummary = string.IsNullOrWhiteSpace(input.BanglaSummary) ? null : input.BanglaSummary.Trim();
            existing.BanglaContentHtml = string.IsNullOrWhiteSpace(input.BanglaContentHtml) ? null : input.BanglaContentHtml;
            existing.Author = string.IsNullOrWhiteSpace(input.Author) ? "JAS Editorial Team" : input.Author.Trim();
            existing.FeaturedImageUrl = string.IsNullOrWhiteSpace(input.FeaturedImageUrl) ? null : input.FeaturedImageUrl.Trim();
            existing.MetaTitle = string.IsNullOrWhiteSpace(input.MetaTitle) ? null : input.MetaTitle.Trim();
            existing.MetaDescription = string.IsNullOrWhiteSpace(input.MetaDescription) ? null : input.MetaDescription.Trim();
            existing.IsPublished = input.IsPublished;
            existing.PublishedAt = input.PublishedAt;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            guide = existing;
        }

        await db.SaveChangesAsync();
        return (Result<AdminSaveResult>)new AdminSaveResult(guide.Id, $"/admin/guides/{guide.Id}/edit");
    });

    public Task<Result> DeleteGuideAsync(int id) => Execute(async service =>
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.Guides.SingleOrDefaultAsync(g => g.Id == id);
        if (existing is not null)
        {
            existing.IsDeleted = true;
            existing.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        return (Result)Success.Ok;
    });

    public Task<Result<string>> UploadAsync(Stream stream, string fileName, string contentType, bool brandLogo) => Execute<string>(async service =>
    {
        var limit = (brandLogo ? 5 : 10) * 1024 * 1024;
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > limit) return (Result<string>)new ValidationError($"Image exceeds {limit / 1024 / 1024}MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read));
        }
        if (buffer.Length == 0) return (Result<string>)new ValidationError("Select an image.");
        buffer.Position = 0;
        await using var scope = scopes.CreateAsyncScope();
        try { return (Result<string>)await ImageUploadValidator.UploadAsync(scope.ServiceProvider.GetRequiredService<IStorageService>(), buffer, fileName, contentType, CancellationToken.None); }
        catch (Exception error) when (error is not OperationCanceledException) { return (Result<string>)new ValidationError("The image could not be uploaded. Check its format and try again."); }
    });
}