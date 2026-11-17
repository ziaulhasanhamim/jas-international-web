using System.Text;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

// Every public operation is called through AdminSession by the workspace backend.
public sealed class ProductWorkspaceService(AppDbContext db, HybridCache cache, Microsoft.AspNetCore.OutputCaching.IOutputCacheStore output)
{
    public async Task<ProductWorkspace> LoadAsync(int productId)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId)
            ?? throw new AdminCatalogException(404, "Product not found.");
        var attributes = await db.ProductAttributeDefinitions.AsNoTracking().Include(a => a.AttributeDefinition).Include(a => a.Options)
            .Where(a => a.ProductId == productId).OrderBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync();
        var variants = await db.ProductVariants.AsNoTracking().Include(v => v.Options).Where(v => v.ProductId == productId)
            .OrderByDescending(v => v.IsDefault).ThenBy(v => v.Id).ToListAsync();
        var inventory = await db.InventoryItems.AsNoTracking().Where(i => i.ProductVariant!.ProductId == productId).ToDictionaryAsync(i => i.ProductVariantId);
        var suggestions = await db.AttributeDefinitions.AsNoTracking().OrderBy(a => a.Name)
            .Select(a => new AttributeSuggestion(a.Id, a.Name, a.Options.OrderBy(o => o.Value).Select(o => new ValueSuggestion(o.Id, o.Value)).ToList())).ToListAsync();
        return new(productId, product.Name, product.CatalogRevision,
            attributes.Select(a => new WorkspaceAttribute
            {
                DefinitionId = a.AttributeDefinitionId,
                Name = a.AttributeDefinition!.Name,
                Required = a.IsRequired,
                Options = a.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Id).Select(o => new WorkspaceOption { Id = o.Id, Value = o.Value, Code = o.Code, SuggestionId = o.AttributeOptionId }).ToList()
            }).ToList(),
            variants.Select(v => new WorkspaceVariant
            {
                Id = v.Id,
                Revision = product.CatalogRevision,
                Sku = v.SKU,
                Barcode = v.Barcode,
                SellingPrice = v.SellingPrice,
                CostPrice = v.CostPrice,
                CompareAtPrice = v.CompareAtPrice,
                Active = v.IsActive,
                Default = v.IsDefault,
                Archived = v.IsDeleted,
                OnHand = inventory.GetValueOrDefault(v.Id)?.QuantityOnHand ?? 0,
                Reserved = inventory.GetValueOrDefault(v.Id)?.QuantityReserved ?? 0,
                Choices = v.Options.ToDictionary(o => attributes.Single(a => a.Id == o.ProductAttributeDefinitionId).AttributeDefinitionId, o => o.ProductAttributeOptionId)
            }).ToList(), suggestions);
    }

    private async Task<Product> LockAsync(int id, long revision)
    {
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"Products\" WHERE \"Id\" = {id} FOR UPDATE");
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id) ?? throw new AdminCatalogException(404, "Product not found.");
        if (product.CatalogRevision != revision) throw new AdminCatalogException(409, "This product changed in another session. Your draft is retained. Reload before applying it again.");
        return product;
    }

    public static string NormalizeValue(string value) => value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();

    public async Task<ProductWorkspace> SaveConfigurationAsync(int id, ProductConfiguration command)
    {
        if (command.Attributes is null || command.Mappings is null || command.Attributes.Any(a => a is null || a.Options is null || a.Options.Any(o => o is null)) || command.Mappings.Any(v => v is null || v.Choices is null) || command.Mappings.Count > 1000 || command.Attributes.Count > 50 || command.Attributes.Select(a => a.DefinitionId).Distinct().Count() != command.Attributes.Count)
            throw new AdminCatalogException(400, "Use at most 50 distinct attributes.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var product = await LockAsync(id, command.Revision);
        var definitions = await db.AttributeDefinitions.Include(a => a.Options).ToDictionaryAsync(a => a.Id);
        var current = await db.ProductAttributeDefinitions.Include(a => a.Options).Where(a => a.ProductId == id).ToListAsync();
        var variants = await db.ProductVariants.Include(v => v.Options).Where(v => v.ProductId == id).ToListAsync();
        var originalOptions = current.SelectMany(a => a.Options).ToDictionary(o => o.Id);
        Dictionary<int, ProductAttributeOption> mapped = [];
        HashSet<int> keys = [];
        for (var index = 0; index < command.Attributes.Count; index++)
        {
            var row = command.Attributes[index];
            if (!definitions.TryGetValue(row.DefinitionId, out var definition)) throw new AdminCatalogException(400, "Select an existing attribute definition.");
            if (row.Options.Count is 0 or > 100) throw new AdminCatalogException(400, "Each attribute needs between 1 and 100 values.");
            if (row.Options.Select(o => NormalizeValue(o.Value)).Distinct().Count() != row.Options.Count) throw new AdminCatalogException(400, "Values must be unique within an attribute.");
            var binding = current.SingleOrDefault(a => a.AttributeDefinitionId == row.DefinitionId);
            if (binding is null)
            {
                binding = new() { ProductId = id, AttributeDefinitionId = row.DefinitionId };
                db.ProductAttributeDefinitions.Add(binding);
            }
            binding.IsRequired = row.Required;
            binding.SortOrder = index;
            for (var position = 0; position < row.Options.Count; position++)
            {
                var option = row.Options[position];
                if (option.Id == 0 || !keys.Add(option.Id) || string.IsNullOrWhiteSpace(option.Value) || option.Value.Trim().Length > 200)
                    throw new AdminCatalogException(400, "Each value needs a unique identity and a label of 1–200 characters.");
                ProductAttributeOption entity;
                if (option.Id > 0)
                {
                    if (!originalOptions.TryGetValue(option.Id, out var owned) || owned.AttributeDefinitionId != row.DefinitionId)
                        throw new AdminCatalogException(400, "An option belongs to a different product or attribute.");
                    entity = owned;
                }
                else
                {
                    if (option.SuggestionId is int source && !definition.Options.Any(o => o.Id == source)) throw new AdminCatalogException(400, "The suggestion belongs to another attribute.");
                    entity = new() { ProductId = id, AttributeDefinitionId = row.DefinitionId, AttributeOptionId = option.SuggestionId };
                    var code = EditorSlug.FromName(option.Value);
                    if (string.IsNullOrWhiteSpace(code)) code = "value";
                    entity.Code = code.Length > 100 ? code[..100] : code;
                    var suffix = 2;
                    var baseCode = entity.Code;
                    while (binding.Options.Any(o => o.Code == entity.Code)) entity.Code = $"{baseCode}-{suffix++}";
                    binding.Options.Add(entity);
                }
                entity.Value = option.Value.Trim();
                entity.NormalizedValue = NormalizeValue(option.Value);
                entity.SortOrder = position;
                mapped.Add(option.Id, entity);
            }
        }
        // Assign database identities before translating temporary UI keys. Transaction rollback protects partial work.
        await db.SaveChangesAsync();
        if (command.Mappings.Select(v => v.Id).Distinct().Count() != variants.Count || command.Mappings.Count != variants.Count)
            throw new AdminCatalogException(400, "Reload the complete variant list before changing configuration.");
        HashSet<string> signatures = [];
        foreach (var variant in variants)
        {
            var mapping = command.Mappings.SingleOrDefault(v => v.Id == variant.Id) ?? throw new AdminCatalogException(400, "Variant mapping is missing.");
            if (variant.IsDeleted && (mapping.Choices.Count != variant.Options.Count || variant.Options.Any(o => !mapping.Choices.Values.Contains(o.ProductAttributeOptionId))))
                throw new AdminCatalogException(409, "Archived variant configuration must be preserved.");
            var selected = new List<ProductAttributeOption>();
            foreach (var attribute in command.Attributes)
            {
                var key = mapping.Choices.GetValueOrDefault(attribute.DefinitionId);
                if (key == 0 && (!attribute.Required || !variant.IsActive || variant.IsDeleted)) continue;
                if (!mapped.TryGetValue(key, out var value) || value.AttributeDefinitionId != attribute.DefinitionId)
                    throw new AdminCatalogException(400, $"Choose a value for {attribute.Name} on variant {variant.SKU}.");
                selected.Add(value);
            }
            if (mapping.Choices.Any(choice => choice.Value != 0 && !command.Attributes.Any(a => a.DefinitionId == choice.Key)))
                throw new AdminCatalogException(400, "Remove obsolete attribute selections before saving.");
            var signature = string.Join("-", selected.Select(o => o.Id).Order());
            if (!variant.IsDeleted && !signatures.Add(signature)) throw new AdminCatalogException(409, "Two variants have the same combination. Map them to different values first.");
            db.ProductVariantOptions.RemoveRange(variant.Options);
            variant.Options.Clear();
            variant.OptionSignature = signature;
            foreach (var option in selected) variant.Options.Add(new() { ProductId = id, ProductAttributeDefinitionId = option.ProductAttributeDefinitionId, ProductAttributeOptionId = option.Id });
        }
        await db.SaveChangesAsync();
        var retained = mapped.Values.Select(o => o.Id).ToHashSet();
        foreach (var option in originalOptions.Values.Where(o => !retained.Contains(o.Id)))
        {
            if (await db.ProductVariantOptions.AnyAsync(v => v.ProductAttributeOptionId == option.Id)) throw new AdminCatalogException(409, "A historical variant still references this value.");
            db.ProductAttributeOptions.Remove(option);
        }
        foreach (var binding in current.Where(a => !command.Attributes.Any(row => row.DefinitionId == a.AttributeDefinitionId))) db.ProductAttributeDefinitions.Remove(binding);
        product.CatalogRevision++;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await RefreshAsync(id);
    }

    public async Task<ProductWorkspace> SaveVariantAsync(int id, WorkspaceVariant command)
    {
        if (command.Id <= 0 || string.IsNullOrWhiteSpace(command.Sku) || command.Sku.Length > 100 || command.Barcode?.Length > 100)
            throw new AdminCatalogException(400, "Enter a valid SKU and barcode of at most 100 characters.");
        foreach (var price in new decimal?[] { command.SellingPrice, command.CostPrice, command.CompareAtPrice })
            if (price is decimal value && (value < 0 || value > 10_000_000 || decimal.Round(value, 2) != value)) throw new AdminCatalogException(400, "Prices must be non-negative with at most two decimal places.");
        if (command.CompareAtPrice is decimal compare && compare <= command.SellingPrice) throw new AdminCatalogException(400, "Compare-at price must exceed selling price, or be left empty.");
        if (command.Active && command.SellingPrice <= 0) throw new AdminCatalogException(400, "Active variants need a positive selling price.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var product = await LockAsync(id, command.Revision);
        var variant = await db.ProductVariants.Include(v => v.Options).SingleOrDefaultAsync(v => v.Id == command.Id && v.ProductId == id)
            ?? throw new AdminCatalogException(404, "Variant not found.");
        if (await db.ProductVariants.AnyAsync(v => v.Id != variant.Id && v.SKU == command.Sku.Trim())) throw new AdminCatalogException(409, "This SKU is already used.");
        if (!string.IsNullOrWhiteSpace(command.Barcode) && await db.ProductVariants.AnyAsync(v => v.Id != variant.Id && v.Barcode == command.Barcode.Trim())) throw new AdminCatalogException(409, "This barcode is already used.");
        if (command.Default && (!command.Active || command.Archived)) throw new AdminCatalogException(400, "The default variant must be active and not archived.");
        var selectedAttributes = variant.Options.Select(o => o.ProductAttributeDefinitionId).ToList();
        if (command.Active && !command.Archived && await db.ProductAttributeDefinitions.AnyAsync(a => a.ProductId == id && a.IsRequired && !selectedAttributes.Contains(a.Id)))
            throw new AdminCatalogException(400, "Map required attribute values before activating this variant.");
        if (!command.Default && variant.IsDefault && !await db.ProductVariants.AnyAsync(v => v.ProductId == id && v.Id != variant.Id && v.IsDefault && v.IsActive && !v.IsDeleted))
            throw new AdminCatalogException(400, "Choose another active default before archiving or deactivating this variant.");
        if (command.Archived && await db.InventoryItems.AnyAsync(i => i.ProductVariantId == variant.Id && (i.QuantityReserved > 0 || i.QuantityOnHand > 0)))
            throw new AdminCatalogException(409, "A variant with stock or reservations cannot be archived. Adjust stock explicitly first.");
        if (command.Default) await db.ProductVariants.Where(v => v.ProductId == id && v.Id != variant.Id).ExecuteUpdateAsync(update => update.SetProperty(v => v.IsDefault, false));
        variant.SKU = command.Sku.Trim(); variant.Barcode = string.IsNullOrWhiteSpace(command.Barcode) ? null : command.Barcode.Trim();
        variant.SellingPrice = command.SellingPrice; variant.CostPrice = command.CostPrice; variant.CompareAtPrice = command.CompareAtPrice;
        variant.IsActive = command.Active && !command.Archived; variant.IsDeleted = command.Archived; variant.IsDefault = command.Default;
        product.CatalogRevision++;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return await RefreshAsync(id);
    }

    public async Task<ProductWorkspace> AdjustStockAsync(int id, WorkspaceStock command)
    {
        if (command.Quantity < 0 || string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500) throw new AdminCatalogException(400, "Enter a non-negative quantity and a reason of 1–500 characters.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var product = await LockAsync(id, command.Revision);
        if (!await db.ProductVariants.AnyAsync(v => v.ProductId == id && v.Id == command.VariantId)) throw new AdminCatalogException(404, "Variant not found.");
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {command.VariantId} FOR UPDATE");
        var stock = await db.InventoryItems.SingleOrDefaultAsync(i => i.ProductVariantId == command.VariantId);
        if ((stock?.QuantityOnHand ?? 0) != command.ExpectedOnHand) throw new AdminCatalogException(409, "Stock changed since this editor loaded. Reload before adjusting it.");
        if (command.Quantity < (stock?.QuantityReserved ?? 0)) throw new AdminCatalogException(409, "Stock cannot fall below reserved units.");
        stock ??= new InventoryItem { ProductVariantId = command.VariantId };
        if (stock.Id == 0) db.InventoryItems.Add(stock);
        var change = command.Quantity - stock.QuantityOnHand;
        stock.QuantityOnHand = command.Quantity;
        if (change != 0) db.InventoryTransactions.Add(new() { ProductVariantId = command.VariantId, QuantityChange = change, Type = InventoryTransactionType.Adjustment, ReferenceType = "AdminStock", Note = command.Reason.Trim() });
        product.CatalogRevision++;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return await RefreshAsync(id);
    }

    private async Task<List<List<ProductAttributeOption>>> CombinationsAsync(int id)
    {
        var attributes = await db.ProductAttributeDefinitions.Include(a => a.Options).Where(a => a.ProductId == id).OrderBy(a => a.SortOrder).ToListAsync();
        List<List<ProductAttributeOption>> combinations = [[]];
        foreach (var attribute in attributes)
        {
            var values = attribute.Options.Cast<ProductAttributeOption?>().ToList();
            if (!attribute.IsRequired) values.Add(null);
            if (values.Count == 0 || combinations.Count * values.Count > 100) throw new AdminCatalogException(400, "Choose values producing at most 100 combinations.");
            combinations = [.. combinations.SelectMany(row => values.Select(value => value is null ? row.ToList() : new List<ProductAttributeOption>([.. row, value])))];
        }
        return combinations;
    }
    public async Task<CombinationPreview> PreviewAsync(int id, long revision)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id) ?? throw new AdminCatalogException(404, "Product not found.");
        if (product.CatalogRevision != revision) throw new AdminCatalogException(409, "Configuration changed. Reload before generating variants.");
        var combinations = await CombinationsAsync(id);
        var existing = await db.ProductVariants.Where(v => v.ProductId == id && !v.IsDeleted).Select(v => v.OptionSignature).ToListAsync();
        return new(combinations.Count, combinations.Count(row => !existing.Contains(string.Join("-", row.Select(o => o.Id).Order()))));
    }
    public async Task<ProductWorkspace> GenerateAsync(int id, long revision)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var product = await LockAsync(id, revision);
        var combinations = await CombinationsAsync(id);
        var existing = await db.ProductVariants.Where(v => v.ProductId == id && !v.IsDeleted).Select(v => v.OptionSignature).ToListAsync();
        foreach (var row in combinations)
        {
            var signature = string.Join("-", row.Select(o => o.Id).Order());
            if (existing.Contains(signature)) continue;
            var suffix = string.Join("-", row.Select(option => option.Code));
            if (suffix.Length > 60) suffix = suffix[..60].TrimEnd('-');
            var baseSku = $"JAS-{id}-{(suffix.Length == 0 ? "BASE" : suffix.ToUpperInvariant())}";
            var sku = baseSku;
            for (var counter = 2; await db.ProductVariants.AnyAsync(variant => variant.SKU == sku); counter++) sku = $"{baseSku}-{counter}";
            db.ProductVariants.Add(new()
            {
                ProductId = id,
                SKU = sku,
                IsActive = false,
                IsDefault = false,
                OptionSignature = signature,
                Options = row.Select(o => new ProductVariantOption { ProductId = id, ProductAttributeDefinitionId = o.ProductAttributeDefinitionId, ProductAttributeOptionId = o.Id }).ToList()
            });
        }
        product.CatalogRevision++;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return await RefreshAsync(id);
    }
    private async Task<ProductWorkspace> RefreshAsync(int id)
    {
        await cache.RemoveByTagAsync(CatalogService.TagProducts); await cache.RemoveByTagAsync(CatalogService.TagSections); await cache.RemoveByTagAsync(CatalogService.TagCatalog);
        await output.EvictByTagAsync("catalog", default);
        db.ChangeTracker.Clear();
        return await LoadAsync(id);
    }
}
