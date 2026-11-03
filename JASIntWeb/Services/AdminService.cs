using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
namespace JASIntWeb.Services;
// Called through AdminSession, which authorizes every operation in a fresh service scope.

public sealed class AdminService(AppDbContext db, UserManager<User> users, IMemoryCache cache, AccountMailService mailService)
{
    private static void Validate(object input)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, true))
        {
            throw new ShopException(string.Join(" ", errors.Select(e => e.ErrorMessage)));
        }
    }

    private static string Code(string value)
    {
        value = value.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
        {
            throw new ShopException("Codes and slugs use lowercase letters, digits and hyphens.");
        }
        return value;
    }

    private static string Url(string? value, bool optional = false)
    {
        value = value?.Trim() ?? "";
        if (optional && value.Length == 0)
        {
            return value;
        }
        if (value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\'))
        {
            return value;
        }
        if (Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == "https")
        {
            return value;
        }
        throw new ShopException("Use a site-relative path or HTTPS URL.");
    }

    private static void Price(decimal value)
    {
        if (value < 0 || value > 10000000 || decimal.Round(value, 2) != value)
        {
            throw new ShopException("Prices must be non-negative with at most two decimal places.");
        }
    }

    private Task LockProductAsync(int id) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"Products\" WHERE \"Id\" = {id} FOR UPDATE");

    public async Task<int> SaveProductAsync(ProductEditForm input)
    {
        Validate(input);
        var p = input.Id == 0 ? new Product
        {
            Name = "",
            Slug = "",
            Details = "",
            MainImageUrl = "",
            CategoryId = 0
        }
        : (await db.Products.Include(p => p.ProductImages)
            .SingleOrDefaultAsync(p => p.Id == input.Id)) ?? throw new ShopException("Product not found. Return to the product list.");
        p.Name = input.Name.Trim();
        p.Slug = Code(input.Slug);
        p.Description = input.Description?.Trim();
        p.Details = input.Details.Trim();
        p.MainImageUrl = Url(input.Image);
        p.CategoryId = input.CategoryId;
        if (input.BrandId.HasValue && input.BrandId.Value > 0)
        {
            if (!await db.Brands.AnyAsync(b => b.Id == input.BrandId.Value && !b.IsDeleted))
            {
                throw new ShopException("Select a valid brand.");
            }
            p.BrandId = input.BrandId.Value;
        }
        else
        {
            p.BrandId = null;
        }
        p.IsDeleted = input.Hidden;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await db.Categories.AnyAsync(c => c.Id == p.CategoryId && !c.IsDeleted && (c.ParentCategory == null || !c.ParentCategory.IsDeleted)))
        {
            throw new ShopException("Select a visible category.");
        }
        if (await db.Products.AnyAsync(x => x.Slug == p.Slug && x.Id != p.Id))
        {
            throw new ShopException("This product slug already exists.");
        }
        var images = (input.Images ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToArray();
        if (images.Length > 20)
        {
            throw new ShopException("Use at most 20 gallery images.");
        }
        foreach (var image in images)
        {
            Url(image);
        }
        if (input.Id == 0)
        {
            db.Products.Add(p);
        }
        db.ProductImages.RemoveRange(p.ProductImages);
        p.ProductImages.Clear();
        for (var i = 0; i < images.Length; i++)
        {
            p.ProductImages.Add(new ProductImage
                {
                    ProductId = p.Id,
                    ImageUrl = images[i],
                    SortOrder = i
                });
        }
        await db.SaveChangesAsync();
        return p.Id;
    }

    public async Task<int> SaveVariantAsync(VariantEditForm input)
    {
        Validate(input);
        Price(input.SellingPrice);
        Price(input.CostPrice);
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockProductAsync(input.ProductId);
        if (!await db.Products.AnyAsync(p => p.Id == input.ProductId))
        {
            throw new ShopException("Product not found.");
        }
        if (input.Id != 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {input.Id} FOR UPDATE");
        }
        var definitions = await db.ProductAttributeDefinitions.Where(a => a.ProductId == input.ProductId).ToListAsync();
        if (input.Choices.Count != definitions.Count
            || input.Choices.Select(c => c.ProductAttributeDefinitionId)
            .Distinct()
            .Count() != definitions.Count
            ||
            input.Choices.Any(c => !definitions.Any(a => a.Id == c.ProductAttributeDefinitionId)))
        {
            throw new ShopException("Product options changed. Reload this page before saving.");
        }
        var ids = input.Choices.Where(c => c.ProductAttributeOptionId > 0).Select(c => c.ProductAttributeOptionId).ToList();
        var options = await db.ProductAttributeOptions.Where(o => o.ProductId == input.ProductId && ids.Contains(o.Id))
            .ToListAsync();
        if (options.Count != ids.Count || definitions.Any(a => a.IsRequired && !options.Any(o => o.ProductAttributeDefinitionId == a.Id))
            ||
            input.Choices.Any(c => c.ProductAttributeOptionId != 0
                && !options.Any(o => o.Id == c.ProductAttributeOptionId
                    && o.ProductAttributeDefinitionId == c.ProductAttributeDefinitionId)))
        {
            throw new ShopException("Choose one allowed value for every required attribute.");
        }
        var signature = string.Join("-", ids.Order());
        if (await db.ProductVariants.AnyAsync(v => v.ProductId == input.ProductId
                && v.Id != input.Id
                && !v.IsDeleted
                && v.OptionSignature == signature))
        {
            throw new ShopException("That combination already exists. Edit its variant instead.");
        }
        var v = input.Id == 0 ? new ProductVariant
        {
            ProductId = input.ProductId,
            SKU = "",
            IsDefault = false
        }
        : (await db.ProductVariants.Include(v => v.Options)
            .SingleOrDefaultAsync(v => v.Id == input.Id
                && v.ProductId == input.ProductId)) ?? throw new ShopException("Variant not found. Return to the variant list.");
        v.SKU = input.Sku.Trim();
        v.SellingPrice = input.SellingPrice;
        v.CostPrice = input.CostPrice;
        v.IsActive = input.Active;
        v.IsDefault = input.Default;
        v.OptionSignature = signature;
        v.UpdatedAt = DateTimeOffset.UtcNow;
        if (await db.ProductVariants.AnyAsync(x => x.SKU == v.SKU && x.Id != v.Id))
        {
            throw new ShopException("This SKU is already in use.");
        }
        if (v.IsDefault)
        {
            if (!v.IsActive)
            {
                throw new ShopException("The default variant must be active.");
            }
            await db.ProductVariants.Where(x => x.ProductId == input.ProductId
                && x.Id != v.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false));
        }
        else if (!await db.ProductVariants.AnyAsync(x => x.ProductId == input.ProductId && x.Id != v.Id && x.IsDefault && x.IsActive && !x.IsDeleted))
        {
            throw new ShopException("Select an active default variant. The first variant must be the default.");
        }
        db.ProductVariantOptions.RemoveRange(v.Options);
        v.Options.Clear();
        if (v.Id != 0)
        {
            await db.SaveChangesAsync();
        }
        foreach (var o in options)
        {
            v.Options.Add(new()
                {
                    ProductId = input.ProductId,
                    ProductAttributeDefinitionId = o.ProductAttributeDefinitionId,
                    ProductAttributeOptionId = o.Id
                });
        }
        if (v.Id == 0)
        {
            db.ProductVariants.Add(v);
        }
        await db.SaveChangesAsync();
        await SetStockCoreAsync(v.Id, input.Stock, "Variant editor stock count");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return v.Id;
    }

    private async Task SetStockCoreAsync(int variantId, int quantity, string reason)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {variantId} FOR UPDATE");
        var inventory = await db.InventoryItems.SingleOrDefaultAsync(i => i.ProductVariantId == variantId);
        if (inventory is null)
        {
            inventory = new()
            {
                ProductVariantId = variantId
            };
            db.InventoryItems.Add(inventory);
        }
        if (quantity < inventory.QuantityReserved)
        {
            throw new ShopException($"Stock cannot be below {inventory.QuantityReserved} units already reserved for orders.");
        }
        var delta = quantity - inventory.QuantityOnHand;
        inventory.QuantityOnHand = quantity;
        if (delta != 0)
        {
            db.InventoryTransactions.Add(new()
                {
                    ProductVariantId = variantId,
                    QuantityChange = delta,
                    Type = InventoryTransactionType.Adjustment,
                    ReferenceType = "AdminStock",
                    Note = reason
                });
        }
    }

    public async Task SaveStockAsync(StockEditForm input)
    {
        Validate(input);
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!await db.ProductVariants.AnyAsync(v => v.Id == input.VariantId))
        {
            throw new ShopException("Variant not found.");
        }
        await SetStockCoreAsync(input.VariantId, input.Quantity, input.Reason.Trim());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task SaveProductAttributesAsync(ProductAttributesForm input)
    {
        if (input.Attributes.Count > 500
            || input.Attributes.Select(a => a.AttributeDefinitionId)
            .Distinct()
            .Count() != input.Attributes.Count)
        {
            throw new ShopException("Invalid product attribute selection.");
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockProductAsync(input.ProductId);
        if (!await db.Products.AnyAsync(p => p.Id == input.ProductId))
        {
            throw new ShopException("Product not found.");
        }
        var definitions = await db.AttributeDefinitions.AsNoTracking().Select(a => a.Id).ToListAsync();
        var globalOptions = await db.AttributeOptions.AsNoTracking().ToDictionaryAsync(o => o.Id);
        var current = await db.ProductAttributeDefinitions.Include(a => a.Options)
            .Where(a => a.ProductId == input.ProductId)
            .ToListAsync();
        var used = await db.ProductVariantOptions.Where(o => o.ProductId == input.ProductId)
            .Select(o => o.ProductAttributeOptionId)
            .ToListAsync();
        var wanted = input.Attributes.Where(a => a.Enabled).ToList();
        if (wanted.Count > 50)
        {
            throw new ShopException("Use at most 50 attributes per product.");
        }
        foreach (var row in wanted)
        {
            if (!definitions.Contains(row.AttributeDefinitionId))
            {
                throw new ShopException("Attribute no longer exists.");
            }
            var selected = row.Options.Where(o => o.Enabled).Select(o => o.AttributeOptionId).ToList();
            if (selected.Count == 0
                || selected.Count > 100
                || selected.Distinct()
                .Count() != selected.Count
                || selected.Any(id => !globalOptions.TryGetValue(id, out var o)
                    || o.AttributeDefinitionId != row.AttributeDefinitionId))
            {
                throw new ShopException("Select at least one valid option for each enabled attribute.");
            }
            var binding = current.FirstOrDefault(a => a.AttributeDefinitionId == row.AttributeDefinitionId);
            if (binding is null)
            {
                binding = new()
                {
                    ProductId = input.ProductId,
                    AttributeDefinitionId = row.AttributeDefinitionId,
                    SortOrder = current.Count
                };
                current.Add(binding);
                db.ProductAttributeDefinitions.Add(binding);
                await db.SaveChangesAsync();
            }
            if (row.Required
                && await db.ProductVariants.AnyAsync(v => v.ProductId == input.ProductId
                    && v.IsActive
                    && !v.IsDeleted
                    && !v.Options.Any(o => o.ProductAttributeDefinitionId == binding.Id)))
            {
                throw new ShopException("An active variant is missing this attribute. Add it as optional first, update the variants, then mark it required.");
            }
            binding.IsRequired = row.Required;
            foreach (var removed in binding.Options.Where(o => !selected.Contains(o.AttributeOptionId)).ToList())
            {
                if (used.Contains(removed.Id))
                {
                    throw new ShopException("An option is still used by a variant. Edit that variant before removing the option.");
                }
                db.ProductAttributeOptions.Remove(removed);
            }
            foreach (var id in selected.Where(id => !binding.Options.Any(o => o.AttributeOptionId == id)))
            {
                binding.Options.Add(new()
                    {
                        ProductId = input.ProductId,
                        AttributeDefinitionId = row.AttributeDefinitionId,
                        AttributeOptionId = id,
                        SortOrder = binding.Options.Count
                    });
            }
        }
        foreach (var removed in current.Where(a => !wanted.Any(w => w.AttributeDefinitionId == a.AttributeDefinitionId)))
        {
            if (removed.Options.Any(o => used.Contains(o.Id)))
            {
                throw new ShopException("An attribute is still used by a variant. Edit its variants before removing it.");
            }
            db.ProductAttributeDefinitions.Remove(removed);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<int> GenerateVariantsAsync(VariantGenerateForm input)
    {
        Validate(input);
        Price(input.SellingPrice);
        Price(input.CostPrice);
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockProductAsync(input.ProductId);
        if (!await db.Products.AnyAsync(p => p.Id == input.ProductId))
        {
            throw new ShopException("Product not found.");
        }
        var attributes = await db.ProductAttributeDefinitions.Include(a => a.Options)
            .Where(a => a.ProductId == input.ProductId)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Id)
            .ToListAsync();
        if (input.Groups.Count != attributes.Count
            || input.Groups.Select(g => g.ProductAttributeDefinitionId)
            .Distinct()
            .Count() != attributes.Count)
        {
            throw new ShopException("Product attributes changed. Reload this page.");
        }
        var combinations = new List<List<ProductAttributeOption>>
        {
            new()
        };
        foreach (var attribute in attributes)
        {
            var group = input.Groups.SingleOrDefault(g => g.ProductAttributeDefinitionId == attribute.Id) ?? throw new ShopException("Invalid attribute.");
            var ids = group.Options.Where(o => o.Selected).Select(o => o.ProductAttributeOptionId).Distinct().ToList();
            if (ids.Any(id => !attribute.Options.Any(o => o.Id == id)) || (group.IncludeNotApplicable && attribute.IsRequired))
            {
                throw new ShopException("Invalid option selection.");
            }
            var values = attribute.Options.Where(o => ids.Contains(o.Id)).Cast<ProductAttributeOption?>().ToList();
            if (group.IncludeNotApplicable && !attribute.IsRequired)
            {
                values.Add(null);
            }
            if (values.Count == 0)
            {
                throw new ShopException("Choose values for every attribute.");
            }
            if (combinations.Count * values.Count > 100)
            {
                throw new ShopException("Generate at most 100 combinations at a time. Select fewer values.");
            }
            combinations = combinations.SelectMany(c => values.Select(v => v is null ? c.ToList() : c.Append(v)
                    .ToList()))
                .ToList();
        }
        var existing = (await db.ProductVariants.Where(v => v.ProductId == input.ProductId
                && !v.IsDeleted)
            .Select(v => v.OptionSignature)
            .ToListAsync()).ToHashSet();
        var count = 0;
        var needsDefault = !await db.ProductVariants.AnyAsync(v => v.ProductId == input.ProductId && v.IsDefault && v.IsActive && !v.IsDeleted);
        foreach (var combination in combinations)
        {
            var signature = string.Join("-", combination.Select(o => o.Id).Order());
            if (existing.Contains(signature))
            {
                continue;
            }
            var suffix = signature.Length == 0 ? "base" : signature;
            var sku = input.SkuPrefix.Trim() + "-" + suffix;
            if (sku.Length > 100 || await db.ProductVariants.AnyAsync(v => v.SKU == sku))
            {
                throw new ShopException("Generated SKU is too long or already used. Choose a different prefix.");
            }
            var variant = new ProductVariant
            {
                ProductId = input.ProductId,
                SKU = sku,
                OptionSignature = signature,
                SellingPrice = input.SellingPrice,
                CostPrice = input.CostPrice,
                IsActive = true,
                IsDefault = needsDefault && count == 0,
                Options = combination.Select(o => new ProductVariantOption
                    {
                        ProductId = input.ProductId,
                        ProductAttributeDefinitionId = o.ProductAttributeDefinitionId,
                        ProductAttributeOptionId = o.Id
                    })
                    .ToList()
            };
            db.ProductVariants.Add(variant);
            await db.SaveChangesAsync();
            db.InventoryItems.Add(new()
                {
                    ProductVariantId = variant.Id,
                    QuantityOnHand = 0
                });
            count++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return count;
    }

    public async Task ChangeOrderAsync(OrderEditForm input)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        Validate(input);
        var id = input.Id;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = {id} FOR UPDATE");
        var order = (await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.DeliveryAddress)
            .Include(o => o.User)
            .SingleOrDefaultAsync(o => o.Id == id)) ?? throw new ShopException("Order not found.");
        if (!Enum.TryParse<OrderStatus>(input.Status, out var status) || !Enum.IsDefined(status))
        {
            throw new ShopException("Invalid order status.");
        }

        order.CourierName = input.CourierName;
        order.CourierTrackingId = input.CourierTrackingId;
        order.CourierTrackingUrl = input.CourierTrackingUrl;
        order.AdminNotes = input.AdminNotes;

        var statusChanged = status != order.Status;

        if (statusChanged)
        {
            var valid = order.Status switch
            {
                OrderStatus.Pending => status is OrderStatus.Processing or OrderStatus.InTransit or OrderStatus.Cancelled,
                OrderStatus.Processing => status is OrderStatus.InTransit or OrderStatus.Delivered or OrderStatus.Cancelled,
                OrderStatus.InTransit => status is OrderStatus.Delivered or OrderStatus.Cancelled,
                _ => false
            };
            if (!valid)
            {
                throw new ShopException($"Invalid status transition from {order.Status} to {status}. Delivered or cancelled orders cannot be reopened.");
            }
            if (status is OrderStatus.Delivered or OrderStatus.Cancelled)
            {
                foreach (var item in order.Items.OrderBy(i => i.ProductVariantId))
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {item.ProductVariantId} FOR UPDATE");
                    var stock = (await db.InventoryItems.SingleOrDefaultAsync(i => i.ProductVariantId == item.ProductVariantId)) ?? throw new ShopException("Inventory is missing. Review stock before changing this order.");
                    if (stock.QuantityReserved < item.Quantity)
                    {
                        throw new ShopException("Reserved stock is inconsistent. Review inventory before changing this order.");
                    }
                    stock.QuantityReserved -= item.Quantity;
                    if (status == OrderStatus.Delivered)
                    {
                        stock.QuantityOnHand -= item.Quantity;
                        db.InventoryTransactions.Add(new()
                            {
                                ProductVariantId = item.ProductVariantId,
                                QuantityChange = -item.Quantity,
                                Type = InventoryTransactionType.Sale,
                                ReferenceType = "Order",
                                ReferenceId = order.Id,
                                Note = "Order delivered"
                            });
                    }
                }
                foreach (var payment in order.Payments.Where(p => p.Method == PaymentMethod.CashOnDelivery && p.Status == PaymentStatus.Pending))
                {
                    payment.Status = status == OrderStatus.Delivered ? PaymentStatus.Completed : PaymentStatus.Failed;
                }
            }
            order.Status = status;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            if (status == OrderStatus.Delivered)
            {
                order.DeliveredAt = order.UpdatedAt;
            }
        }
        else
        {
            order.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        if (statusChanged)
        {
            // Dispatch order status update email to customer
            try
            {
                await mailService.SendOrderStatusUpdateEmailAsync(order, status);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email] Failed to send order status update email: {ex.Message}");
            }
        }
    }

    public async Task SaveCategoryAsync(CategoryEditForm input)
    {
        Validate(input);
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Categories\" IN SHARE ROW EXCLUSIVE MODE");
        var c = input.Id == 0 ? new Category
        {
            Name = "",
            Slug = ""
        }
        : (await db.Categories.SingleOrDefaultAsync(c => c.Id == input.Id)) ?? throw new ShopException("Category not found.");
        c.Name = input.Name.Trim();
        c.Slug = Code(input.Slug);
        c.Icon = input.Icon?.Trim();
        c.IsDeleted = input.Hidden;
        c.ParentCategoryId = input.ParentId;
        var categories = await db.Categories.AsNoTracking().ToListAsync();
        if (categories.Any(x => x.Slug == c.Slug && x.Id != c.Id))
        {
            throw new ShopException("This category slug already exists.");
        }
        if (c.ParentCategoryId.HasValue)
        {
            if (c.ParentCategoryId == c.Id || !categories.Any(x => x.Id == c.ParentCategoryId && !x.ParentCategoryId.HasValue && !x.IsDeleted))
            {
                throw new ShopException("Select a visible top-level parent category.");
            }
            if (c.Id != 0 && categories.Any(x => x.ParentCategoryId == c.Id))
            {
                throw new ShopException("A category with children must remain at the top level.");
            }
        }
        if (c.Id == 0)
        {
            db.Categories.Add(c);
        }
        await db.SaveChangesAsync();
        var placement = await db.CategoryPlacements.SingleOrDefaultAsync(p => p.CategoryId == c.Id
            && p.PlacementType == CategoryPlacementType.HomepageFeatured);
        if (placement is null)
        {
            placement = new()
            {
                CategoryId = c.Id,
                PlacementType = CategoryPlacementType.HomepageFeatured,
                SortOrder = (await db.CategoryPlacements.MaxAsync(p => (int?)p.SortOrder) ?? -1) + 1
            };
            db.CategoryPlacements.Add(placement);
        }
        placement.IsActive = input.Featured && !input.Hidden;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        cache.Remove(CatalogService.CategoriesCacheKey);
        cache.Remove(CatalogService.HomeCategoriesCacheKey);
    }

    public async Task SaveBrandAsync(BrandEditForm input)
    {
        Validate(input);
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Brands\" IN SHARE ROW EXCLUSIVE MODE");
        var b = input.Id == 0 ? new Brand
        {
            Name = "",
            Slug = ""
        }
        : (await db.Brands.SingleOrDefaultAsync(x => x.Id == input.Id)) ?? throw new ShopException("Brand not found.");
        b.Name = input.Name.Trim();
        b.Slug = Code(input.Slug);
        b.Description = input.Description?.Trim();
        b.LogoUrl = string.IsNullOrWhiteSpace(input.LogoUrl) ? null : Url(input.LogoUrl);
        b.IsFeatured = input.IsFeatured;
        b.SortOrder = input.SortOrder;
        b.IsDeleted = input.Hidden;
        b.UpdatedAt = DateTimeOffset.UtcNow;
        if (await db.Brands.AnyAsync(x => x.Slug == b.Slug && x.Id != b.Id))
        {
            throw new ShopException("This brand slug already exists.");
        }
        if (b.Id == 0)
        {
            db.Brands.Add(b);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        cache.Remove(CatalogService.BrandsCacheKey);
        cache.Remove(CatalogService.FeaturedBrandsCacheKey);
    }

    public async Task ToggleBrandFeaturedAsync(int id)
    {
        var b = await db.Brands.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ShopException("Brand not found.");
        b.IsFeatured = !b.IsFeatured;
        b.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        cache.Remove(CatalogService.BrandsCacheKey);
        cache.Remove(CatalogService.FeaturedBrandsCacheKey);
    }

    public async Task DeleteBrandAsync(int id)
    {
        var b = await db.Brands.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ShopException("Brand not found.");
        b.IsDeleted = true;
        b.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        cache.Remove(CatalogService.BrandsCacheKey);
        cache.Remove(CatalogService.FeaturedBrandsCacheKey);
    }

    public async Task<int> SaveAttributeAsync(AttributeEditForm input)
    {
        Validate(input);
        var code = Code(input.Code);
        var a = input.Id == 0 ? new AttributeDefinition
        {
            Name = "",
            Code = code
        }
        : (await db.AttributeDefinitions.SingleOrDefaultAsync(a => a.Id == input.Id)) ?? throw new ShopException("Attribute not found.");
        if (await db.AttributeDefinitions.AnyAsync(x => x.Code == code && x.Id != input.Id))
        {
            throw new ShopException("Attribute code already exists.");
        }
        a.Name = input.Name.Trim();
        a.Code = code;
        a.Unit = input.Unit?.Trim();
        if (a.Id == 0)
        {
            db.AttributeDefinitions.Add(a);
        }
        await db.SaveChangesAsync();
        return a.Id;
    }

    public async Task SaveOptionAsync(OptionEditForm input)
    {
        Validate(input);
        if (!await db.AttributeDefinitions.AnyAsync(a => a.Id == input.AttributeDefinitionId))
        {
            throw new ShopException("Attribute not found.");
        }
        var o = input.Id == 0 ? new AttributeOption
        {
            AttributeDefinitionId = input.AttributeDefinitionId,
            Value = "",
            Code = ""
        }
        : (await db.AttributeOptions.SingleOrDefaultAsync(o => o.Id == input.Id
                && o.AttributeDefinitionId == input.AttributeDefinitionId)) ?? throw new ShopException("Attribute option not found.");
        o.Value = input.Value.Trim();
        o.Code = Code(input.Code);
        if (await db.AttributeOptions.AnyAsync(x => x.AttributeDefinitionId == o.AttributeDefinitionId
                && x.Id != o.Id
                && (x.Value == o.Value
                    || x.Code == o.Code)))
        {
            throw new ShopException("This attribute already has that value or code.");
        }
        if (o.Id == 0)
        {
            db.AttributeOptions.Add(o);
        }
        await db.SaveChangesAsync();
    }

    public async Task SaveSlideAsync(SlideEditForm input)
    {
        Validate(input);
        var s = input.Id == 0 ? new CarouselSlide
        {
            ImageUrl = "",
            ImageAlt = "",
            SortOrder = (await db.CarouselSlides.MaxAsync(x => (int?)x.SortOrder) ?? -1) + 1
        }
        : (await db.CarouselSlides.SingleOrDefaultAsync(s => s.Id == input.Id)) ?? throw new ShopException("Carousel slide not found.");
        s.Title = input.Title?.Trim();
        s.Eyebrow = input.Eyebrow?.Trim();
        s.Copy = input.Copy?.Trim();
        s.ImageUrl = Url(input.Image);
        s.ImageAlt = input.Alt.Trim();
        s.LinkUrl = Url(input.Link, true);
        s.LinkText = input.LinkText?.Trim();
        s.IsActive = input.Active;
        if (s.Id == 0)
        {
            db.CarouselSlides.Add(s);
        }
        await db.SaveChangesAsync();
        cache.Remove(CatalogService.SlidesCacheKey);
    }

    public async Task DeleteSlideAsync(int id)
    {
        var slide = (await db.CarouselSlides.SingleOrDefaultAsync(s => s.Id == id)) ?? throw new ShopException("Carousel slide not found.");
        db.CarouselSlides.Remove(slide);
        await db.SaveChangesAsync();
        cache.Remove(CatalogService.SlidesCacheKey);
    }

    public async Task ChangeRoleAsync(RoleEditForm input, int actorId)
    {
        if (input.Id == actorId)
        {
            throw new ShopException("Use a different administrator to change your own role.");
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"AspNetUserRoles\" IN SHARE ROW EXCLUSIVE MODE");
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null || !await users.IsInRoleAsync(actor, "Admin"))
        {
            throw new ShopException("Administrator access is required.");
        }
        var user = await users.FindByIdAsync(input.Id.ToString()) ?? throw new ShopException("User not found.");
        if (await users.IsInRoleAsync(user, "Admin") != input.Admin)
        {
            var result = input.Admin ? await users.AddToRoleAsync(user, "Admin") : await users.RemoveFromRoleAsync(user, "Admin");
            if (!result.Succeeded)
            {
                throw new ShopException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }
            var stamp = await users.UpdateSecurityStampAsync(user);
            if (!stamp.Succeeded)
            {
                throw new ShopException("The account changed. Reload before changing access again.");
            }
        }
        await tx.CommitAsync();
    }

    public async Task ReorderAsync(OrderableKind kind, int scopeId, List<int> ids)
    {
        if (ids.Count > 500 || ids.Count != ids.Distinct().Count())
        {
            throw new ShopException("Invalid ordering.");
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        if (kind == OrderableKind.ProductAttributes)
        {
            await LockProductAsync(scopeId);
        }
        if (kind == OrderableKind.ProductOptions)
        {
            var productId = (await db.ProductAttributeDefinitions.Where(a => a.Id == scopeId)
                .Select(a => (int?)a.ProductId)
                .SingleOrDefaultAsync()) ?? throw new ShopException("Product attribute no longer exists. Reload the list.");
            await LockProductAsync(productId);
        }
        void Same(IEnumerable<int> existing)
        {
            if (!existing.ToHashSet().SetEquals(ids))
            {
                throw new ShopException("This list changed. Reload it before saving the order.");
            }
        }
        switch (kind)
        {
            case OrderableKind.ProductAttributes:
            var attributes = await db.ProductAttributeDefinitions.Where(a => a.ProductId == scopeId).ToListAsync();
            Same(attributes.Select(a => a.Id));
            foreach (var a in attributes)
            {
                a.SortOrder = ids.IndexOf(a.Id);
            }
            break;
            case OrderableKind.ProductOptions:
            var options = await db.ProductAttributeOptions.Where(o => o.ProductAttributeDefinitionId == scopeId)
                .ToListAsync();
            Same(options.Select(o => o.Id));
            foreach (var o in options)
            {
                o.SortOrder = ids.IndexOf(o.Id);
            }
            break;
            case OrderableKind.Carousel:
            var slides = await db.CarouselSlides.ToListAsync();
            Same(slides.Select(s => s.Id));
            foreach (var s in slides)
            {
                s.SortOrder = ids.IndexOf(s.Id);
            }
            break;
            case OrderableKind.FeaturedCategories:
            var placements = await db.CategoryPlacements.Where(p => p.IsActive
                && p.PlacementType == CategoryPlacementType.HomepageFeatured
                && !p.Category!.IsDeleted)
                .ToListAsync();
            Same(placements.Select(p => p.Id));
            foreach (var p in placements)
            {
                p.SortOrder = ids.IndexOf(p.Id);
            }
            break;
            default: throw new ShopException("Unknown ordering list.");
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        cache.Remove(CatalogService.SlidesCacheKey);
        cache.Remove(CatalogService.HomeCategoriesCacheKey);
    }

    public async Task<int> CreateUserByAdminAsync(AdminUserCreateForm input)
    {
        Validate(input);
        var email = input.Email.Trim().ToLowerInvariant();
        var existing = await users.FindByEmailAsync(email);
        if (existing != null)
        {
            throw new ShopException("An account with this email address already exists.");
        }

        var user = new User
        {
            Email = email,
            UserName = email,
            EmailConfirmed = input.EmailConfirmed
        };

        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            throw new ShopException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        if (!string.IsNullOrWhiteSpace(input.FullName))
        {
            await users.AddClaimAsync(user, new Claim("name", input.FullName.Trim()));
        }

        if (input.IsAdmin)
        {
            await users.AddToRoleAsync(user, "Admin");
        }

        return user.Id;
    }

    public async Task UpdateUserProfileAsync(AdminUserEditProfileForm input)
    {
        Validate(input);
        var user = await users.FindByIdAsync(input.Id.ToString()) ?? throw new ShopException("User not found.");
        var newEmail = input.Email.Trim().ToLowerInvariant();

        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await users.FindByEmailAsync(newEmail);
            if (existing != null && existing.Id != user.Id)
            {
                throw new ShopException("Another user already has this email address.");
            }
            user.Email = newEmail;
            user.UserName = newEmail;
        }

        var updateResult = await users.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new ShopException(string.Join(" ", updateResult.Errors.Select(e => e.Description)));
        }

        var existingClaims = await users.GetClaimsAsync(user);
        var nameClaim = existingClaims.FirstOrDefault(c => c.Type == "name");
        if (nameClaim != null)
        {
            await users.RemoveClaimAsync(user, nameClaim);
        }
        if (!string.IsNullOrWhiteSpace(input.FullName))
        {
            await users.AddClaimAsync(user, new Claim("name", input.FullName.Trim()));
        }

        await users.UpdateSecurityStampAsync(user);
    }

    public async Task AdminResetPasswordAsync(AdminUserPasswordResetForm input)
    {
        Validate(input);
        var user = await users.FindByIdAsync(input.Id.ToString()) ?? throw new ShopException("User not found.");
        
        if (await users.HasPasswordAsync(user))
        {
            var removeResult = await users.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                throw new ShopException(string.Join(" ", removeResult.Errors.Select(e => e.Description)));
            }
        }

        var addResult = await users.AddPasswordAsync(user, input.NewPassword);
        if (!addResult.Succeeded)
        {
            throw new ShopException(string.Join(" ", addResult.Errors.Select(e => e.Description)));
        }

        await users.UpdateSecurityStampAsync(user);
    }

    public async Task ToggleEmailConfirmationAsync(int userId)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new ShopException("User not found.");
        user.EmailConfirmed = !user.EmailConfirmed;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ShopException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task ToggleLockoutAsync(int userId, int actorId)
    {
        if (userId == actorId)
        {
            throw new ShopException("You cannot lock out your own administrator account.");
        }
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new ShopException("User not found.");
        
        var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
        if (isLocked)
        {
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
        }
        else
        {
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            await users.UpdateSecurityStampAsync(user);
        }
    }

    public async Task<int> CreateCustomerAsync(SignupForm input)
    {
        Validate(input);
        var email = input.Email.Trim();
        var user = new User
        {
            Email = email,
            UserName = email
        };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            throw new ShopException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
        return user.Id;
    }
}
