using System.ComponentModel.DataAnnotations;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
namespace JASIntWeb.Services;

public sealed class ServerAdminCatalogBackend(AdminSession session, IServiceScopeFactory scopes, IConfiguration configuration) : IAdminCatalogBackend
{
    private string PublicBaseUrl => configuration["PublicBaseUrl"] ?? "https://www.jasinternational.com.bd";
    private AdminEditorData<T> Data<T>(T input, List<CategoryLinkDto>? categories = null, List<BrandLinkDto>? brands = null, int children = 0, int products = 0)
        => new(input, categories ?? [], brands ?? [], children, products, PublicBaseUrl);
    private async Task<T> Execute<T>(Func<AdminService, Task<T>> action)
    {
        try { return await session.RunAsync((service, _) => action(service)); }
        catch (ShopException error) { throw new AdminCatalogException(error.Message.Contains("session expired", StringComparison.OrdinalIgnoreCase) ? 401 : 409, error.Message); }
    }
    public Task<AdminEditorData<CategoryEditForm>> CategoryAsync(int Id) => Execute(async service =>
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
            throw new AdminCatalogException(404, "Record not found.");
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

    public Task<AdminEditorData<BrandEditForm>> BrandAsync(int Id) => Execute(async service =>
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
                throw new AdminCatalogException(404, "Record not found.");
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

    public Task<AdminEditorData<ProductEditForm>> ProductAsync(int Id) => Execute(async service =>
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
                Hidden = false,
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
            throw new AdminCatalogException(404, "Record not found.");
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

    private Task<AdminSaveResult> Save<T>(T input, Func<AdminService, Task<int>> save, Func<int, string> next)
    {
        return Execute(async service =>
        {
            List<ValidationResult> errors = [];
            if (!Validator.TryValidateObject(input!, new ValidationContext(input!), errors, true))
            {
                var fields = errors.SelectMany(error => error.MemberNames.Select(name => (name, message: error.ErrorMessage ?? "Invalid value.")))
                    .GroupBy(error => error.name)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.message).ToArray());
                throw new AdminCatalogException(400, "Review the highlighted fields.", fields);
            }
            var id = await save(service);
            return new AdminSaveResult(id, next(id));
        });
    }

    public Task<AdminSaveResult> SaveCategoryAsync(CategoryEditForm input) => Save(input, service => service.SaveCategoryAsync(input), _ => "/admin/categories");
    public Task<AdminSaveResult> SaveBrandAsync(BrandEditForm input) => Save(input, service => service.SaveBrandAsync(input), _ => "/admin/brands");
    public Task<AdminSaveResult> SaveProductAsync(ProductEditForm input) => Save(input, service => service.SaveProductAsync(input), id => input.HasVariants ? $"/admin/products/{id}/variants" : "/admin/products");
    public async Task DeleteBrandAsync(int id) => await Execute(async service =>
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Brands.AnyAsync(b => b.Id == id)) throw new AdminCatalogException(404, "Brand not found.");
        if (await db.Products.AnyAsync(p => p.BrandId == id && !p.IsDeleted)) throw new AdminCatalogException(409, "Brands with products cannot be deleted from this editor.");
        await service.DeleteBrandAsync(id);
        return true;
    });
    public Task<string> UploadAsync(Stream stream, string fileName, string contentType, bool brandLogo) => Execute(async service =>
    {
        var limit = (brandLogo ? 5 : 10) * 1024 * 1024;
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > limit) throw new AdminCatalogException(400, $"Image exceeds {limit / 1024 / 1024}MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read));
        }
        if (buffer.Length == 0) throw new AdminCatalogException(400, "Select an image.");
        buffer.Position = 0;
        await using var scope = scopes.CreateAsyncScope();
        try { return await ImageUploadValidator.UploadAsync(scope.ServiceProvider.GetRequiredService<IStorageService>(), buffer, fileName, contentType, CancellationToken.None); }
        catch (Exception error) when (error is not OperationCanceledException) { throw new AdminCatalogException(400, "The image could not be uploaded. Check its format and try again."); }
    });
}
