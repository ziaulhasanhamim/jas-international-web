using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
namespace JASIntWeb.Services.ProductEditor;

public sealed partial class ProductEditorService
{
    private async Task SaveVariantsAsync(int productId, ProductEditorDraft draft, Dictionary<Guid, ProductAttributeOption> options, Guid operation, int actor, CancellationToken ct)
    {
        var existing = await db.ProductVariants.Include(v => v.Options).Where(v => v.ProductId == productId).ToListAsync(ct);
        if (draft.Variants.Select(v => v.ClientId).Distinct().Count() != draft.Variants.Count || draft.Variants.Any(v => v.ClientId == Guid.Empty)
            || draft.Variants.Where(v => v.Id > 0).Select(v => v.Id).Distinct().Count() != draft.Variants.Count(v => v.Id > 0))
            throw Invalid("Variants", "Each variant needs a distinct identity.");
        if (existing.Where(v => !v.IsDeleted).Any(v => !draft.Variants.Any(row => row.Id == v.Id))) throw Invalid("Variants", "An existing variant was omitted. Archive it explicitly rather than removing its identity.");
        var beforeCount = existing.Count(v => !v.IsDeleted);
        var afterCount = draft.Variants.Count(v => !v.Archived);
        if (afterCount > 100 && (afterCount > beforeCount || draft.Variants.Any(v => !v.Archived && (v.Id == 0 || existing.Any(e => e.Id == v.Id && e.IsDeleted))))) throw Invalid("Variants", "A product supports at most 100 current variants. Archive unwanted variants before adding more.");
        if (draft.Variants.Count(v => !v.Archived && v.Default) > 1) throw Invalid("Default", "Choose only one default variant.");
        HashSet<string> signatures = [];
        HashSet<string> skus = [with(StringComparer.OrdinalIgnoreCase)];
        Dictionary<EditorVariant, List<ProductAttributeOption>> choices = [];
        foreach (var row in draft.Variants)
        {
            var field = $"Variants.{row.ClientId:N}";
            if (row.Id > 0 && !existing.Any(v => v.Id == row.Id)) throw Invalid(field, "This variant belongs to another product.");
            if (row.Choices is null || row.OpeningStock < 0 || row.Id > 0 && row.OpeningStock != 0) throw Invalid(field, "Opening stock is only available for new variants and must be non-negative.");
            var entity = existing.SingleOrDefault(v => v.Id == row.Id);
            if (row.Archived)
            {
                if (entity is null) throw Invalid(field, "Remove an unsaved row instead of archiving it.");
                if (await db.InventoryItems.AnyAsync(i => i.ProductVariantId == row.Id && (i.QuantityOnHand > 0 || i.QuantityReserved > 0), ct))
                    throw Invalid(field, "Stock and reservations must be cleared explicitly before archiving.", 409);
                continue;
            }
            foreach (var price in new[] { row.Price, row.Cost, row.CompareAt })
                if (price is { } value && (value < 0 || value > 10_000_000 || decimal.Round(value, 2) != value)) throw Invalid(field, "Prices need non-negative values with at most two decimal places.");
            if (row.Active && row.Price is not > 0) throw Invalid(field, "Active variants need a positive selling price.");
            if (row.CompareAt is { } compare && compare <= (row.Price ?? 0)) throw Invalid(field, "Compare-at price must exceed the selling price, or be empty.");
            List<ProductAttributeOption> selected = [];
            foreach (var attribute in draft.Attributes.Where(a => !a.Retired))
            {
                var choice = row.Choices.GetValueOrDefault(attribute.DefinitionId);
                if (choice == Guid.Empty)
                { if (attribute.Required && row.Active) throw Invalid(field, $"Choose {attribute.Name} before activating this variant."); continue; }
                if (!options.TryGetValue(choice, out var option) || option.IsRetired || option.AttributeDefinitionId != attribute.DefinitionId)
                    throw Invalid(field, "A selected option is retired or belongs to another product/attribute.");
                selected.Add(option);
            }
            if (row.Choices.Any(choice => choice.Value != Guid.Empty && !draft.Attributes.Any(a => !a.Retired && a.DefinitionId == choice.Key))) throw Invalid(field, "Remove selections for retired attributes.");
            var signature = ProductEditorRules.Signature(selected.Select(o => o.Id));
            if (!signatures.Add(signature)) throw Invalid(field, "Two current variants have the same combination.", 409);
            // Restoring must be explicit; generation must not silently recreate a historical combination.
            if (row.Id == 0 && existing.Any(v => v.IsDeleted && v.OptionSignature == signature)) throw Invalid(field, "Restore the archived matching variant instead of creating a duplicate.", 409);
            choices[row] = selected;
            var sku = string.IsNullOrWhiteSpace(row.Sku) ? ProductEditorRules.AutoSku(draft, row) : row.Sku.Trim();
            if (sku.Length > 100 || string.IsNullOrWhiteSpace(sku)) throw Invalid(field, "SKU must contain 1–100 characters.");
            var candidate = sku;
            for (var suffix = 2; await db.ProductVariants.AnyAsync(v => v.Id != row.Id && v.NormalizedSku == candidate.ToUpper(), ct) || skus.Contains(candidate); suffix++)
            {
                if (!string.IsNullOrWhiteSpace(row.Sku)) throw Invalid(field, "This SKU is already used, including archived variants.", 409);
                candidate = $"{sku}-{suffix}";
                if (candidate.Length > 100) throw Invalid(field, "Unable to allocate a unique SKU.", 409);
            }
            row.Sku = candidate; skus.Add(candidate);
            if (row.Barcode?.Length > 100) throw Invalid(field, "Barcode must be at most 100 characters.");
            if (!string.IsNullOrWhiteSpace(row.Barcode) && (await db.ProductVariants.AnyAsync(v => v.Id != row.Id && v.Barcode == row.Barcode.Trim(), ct)
                || draft.Variants.Any(v => v.ClientId != row.ClientId && v.Barcode?.Trim() == row.Barcode.Trim()))) throw Invalid(field, "This barcode is already used.", 409);
        }
        // Two-phase replacement permits combination swaps and a new default under unique indexes.
        foreach (var row in existing.Where(v => !v.IsDeleted || draft.Variants.Any(d => d.Id == v.Id && !d.Archived)))
        {
            row.IsDefault = false;
            row.OptionSignature = $"~{operation:N}-{row.Id}";
        }
        await db.SaveChangesAsync(ct);
        foreach (var row in draft.Variants)
        {
            var entity = existing.SingleOrDefault(v => v.Id == row.Id);
            if (row.Archived)
            {
                if (entity is not null)
                {
                    entity.IsDeleted = true; entity.IsActive = false; entity.IsDefault = false;
                    entity.OptionSignature = ProductEditorRules.Signature(entity.Options.Select(o => o.ProductAttributeOptionId));
                }
                continue;
            }
            entity ??= new() { ProductId = productId, SKU = row.Sku };
            if (entity.Id > 0) { db.ProductVariantOptions.RemoveRange(entity.Options); entity.Options.Clear(); }
            else db.ProductVariants.Add(entity);
            entity.SKU = row.Sku; entity.Barcode = string.IsNullOrWhiteSpace(row.Barcode) ? null : row.Barcode.Trim();
            entity.SellingPrice = row.Price ?? 0; entity.CostPrice = row.Cost ?? 0; entity.CompareAtPrice = row.CompareAt;
            entity.IsActive = row.Active; entity.IsDefault = row.Default; entity.IsDeleted = false;
            entity.OptionSignature = ProductEditorRules.Signature(choices[row].Select(o => o.Id));
        }
        await db.SaveChangesAsync(ct);
        // Options are inserted after old selections are deleted, avoiding transient FK/unique failures.
        foreach (var row in draft.Variants.Where(v => !v.Archived))
        {
            var entity = db.ChangeTracker.Entries<ProductVariant>().Select(e => e.Entity).Single(v => v.ProductId == productId && v.SKU == row.Sku);
            foreach (var option in choices[row]) entity.Options.Add(new() { ProductId = productId, ProductAttributeDefinitionId = option.ProductAttributeDefinitionId, ProductAttributeOptionId = option.Id });
            if (row.Id == 0)
            {
                db.InventoryItems.Add(new() { ProductVariantId = entity.Id, QuantityOnHand = row.OpeningStock });
                if (row.OpeningStock != 0) db.InventoryTransactions.Add(new() { ProductVariantId = entity.Id, QuantityChange = row.OpeningStock, Type = InventoryTransactionType.Adjustment, ReferenceType = "ProductOpeningStock", Note = "Initial stock", CreatedByUserId = actor });
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
