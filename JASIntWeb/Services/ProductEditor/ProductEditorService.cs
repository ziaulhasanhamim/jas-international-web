using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Client.Services;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;
namespace JASIntWeb.Services.ProductEditor;

internal sealed class ProductEditorException(int status, string message, Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public Dictionary<string, string[]>? Errors { get; } = errors;
}

public sealed partial class ProductEditorService(AppDbContext db, AdminService admin, CatalogService catalog,
    IOutputCacheStore output, IConfiguration configuration, ILogger<ProductEditorService> logger, IStorageService storage)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static ProductEditorException Invalid(string field, string message, int status = 422) => new(status, message, new() { [field] = [message] });
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private async Task<ProductEditorReceipt?> ReceiptAsync(int actor, Guid operation, string? hash, CancellationToken ct)
    {
        var receipt = await db.ProductEditorReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor && r.OperationId == operation, ct);
        if (receipt is not null && hash is not null && receipt.PayloadHash != hash) throw Invalid("Operation", "This operation ID already belongs to a different command.", 409);
        return receipt;
    }
    private async Task LockOperationAsync(int actor, Guid operation, CancellationToken ct)
    {
        if (operation == Guid.Empty) throw Invalid("Operation", "An operation ID is required.", 400);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({$"product-editor:{actor}:{operation}"}, 0))", ct);
    }
    public async Task<EditorCommit?> OperationAsync(int actor, Guid operation, CancellationToken ct)
    {
        var receipt = await ReceiptAsync(actor, operation, null, ct);
        return receipt is null ? null : await CompletedAsync(JsonSerializer.Deserialize<EditorReceipt>(receipt.ResultJson, Json)!, ct);
    }
    private async Task<EditorCommit> CompletedAsync(EditorReceipt receipt, CancellationToken ct)
    {
        try
        {
            await catalog.InvalidateAllAsync();
            foreach (var tag in new[] { "catalog", "seo", "static-content" }) await output.EvictByTagAsync(tag, ct);
            db.ChangeTracker.Clear();
            return new(receipt, await LoadAsync(receipt.ProductId, ct));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            logger.LogWarning(error, "Product command {OperationId} committed but refresh failed", receipt.OperationId);
            return new(receipt, null, "Saved successfully. Reload the saved product to refresh prices and stock; do not create another copy.");
        }
    }
    private void Record(int actor, Guid operation, int id, long revision, string kind, string hash)
    {
        db.ProductEditorReceipts.Add(new() { ActorId = actor, OperationId = operation, ProductId = id, CommandKind = kind, PayloadHash = hash,
            ResultJson = JsonSerializer.Serialize(new EditorReceipt(operation, id, revision), Json) });
    }
    public async Task<ProductEditorData> LoadAsync(int id, CancellationToken ct = default)
    {
        var draft = new ProductEditorDraft { Id = id };
        if (id > 0)
        {
            var p = await db.Products.IgnoreQueryFilters().AsNoTracking().Include(p => p.ProductImages).SingleOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new ProductEditorException(404, "Product not found.");
            draft.Revision = p.CatalogRevision;
            draft.Information = new() { Name = p.Name, Slug = p.Slug, CategoryId = p.CategoryId, BrandId = p.BrandId, Description = p.Description ?? "", Details = p.Details,
                Image = p.MainImageUrl, Images = string.Join('\n', p.ProductImages.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl)), Hidden = p.IsDeleted,
                Featured = p.IsFeatured, NewArrival = p.IsNewArrival, MetaTitle = p.MetaTitle, MetaDescription = p.MetaDescription, OgImageUrl = p.OgImageUrl,
                Synonyms = p.Synonyms, BanglaName = p.BanglaName, BanglaDescription = p.BanglaDescription, WarrantyInfo = p.WarrantyInfo };
            var attributes = await db.ProductAttributeDefinitions.AsNoTracking().Include(a => a.AttributeDefinition).Include(a => a.Options).Where(a => a.ProductId == id).OrderBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync(ct);
            draft.Attributes = attributes.Select(a => new EditorAttribute { DefinitionId = a.AttributeDefinitionId, Name = a.AttributeDefinition!.Name, Required = a.IsRequired,
                Retired = a.IsRetired, Fields = ProductEditorRules.DecodeFields(a.MetadataSchemaJson), SwatchField = a.SwatchFieldKey,
                Options = a.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Id).Select(o => new EditorOption { Id = o.Id, ClientId = ProductEditorRules.Identity("option", o.Id),
                    Value = o.Value, Code = o.Code, SuggestionId = o.AttributeOptionId, Retired = o.IsRetired, Metadata = ProductEditorRules.DecodeMetadata(o.MetadataJson) }).ToList() }).ToList();
            var variants = await db.ProductVariants.AsNoTracking().Include(v => v.Options).Where(v => v.ProductId == id).OrderBy(v => v.IsDeleted).ThenByDescending(v => v.IsDefault).ThenBy(v => v.Id).ToListAsync(ct);
            var inventory = await db.InventoryItems.AsNoTracking().Where(i => i.ProductVariant!.ProductId == id).ToDictionaryAsync(i => i.ProductVariantId, ct);
            var definitions = attributes.ToDictionary(a => a.Id, a => a.AttributeDefinitionId);
            draft.Variants = variants.Select(v => new EditorVariant { Id = v.Id, ClientId = ProductEditorRules.Identity("variant", v.Id), Sku = v.SKU, Barcode = v.Barcode,
                Price = v.SellingPrice, Cost = v.CostPrice, CompareAt = v.CompareAtPrice, Active = v.IsActive, Default = v.IsDefault, Archived = v.IsDeleted,
                OnHand = inventory.GetValueOrDefault(v.Id)?.QuantityOnHand ?? 0, Reserved = inventory.GetValueOrDefault(v.Id)?.QuantityReserved ?? 0,
                InventoryVersion = inventory.GetValueOrDefault(v.Id)?.Version ?? 0,
                Choices = v.Options.ToDictionary(o => definitions[o.ProductAttributeDefinitionId], o => ProductEditorRules.Identity("option", o.ProductAttributeOptionId)) }).ToList();
        }
        var global = await db.AttributeDefinitions.AsNoTracking().Include(a => a.Options).OrderBy(a => a.Name).ToListAsync(ct);
        var templates = global.Select(a => new EditorAttributeTemplate(a.Id, a.Name, ProductEditorRules.DecodeFields(a.MetadataSchemaJson), a.SwatchFieldKey,
            a.Options.OrderBy(o => o.Value).Select(o => new EditorOption { SuggestionId = o.Id, Value = o.Value, Code = o.Code, Metadata = ProductEditorRules.DecodeMetadata(o.MetadataJson) }).ToList())).ToList();
        return new(draft, await catalog.CategoriesAsync(), await catalog.BrandsAsync(), templates, configuration["PublicBaseUrl"] ?? "https://www.jasinternational.com.bd");
    }
    public async Task<EditorCommit> SaveAsync(int actor, EditorSave command, CancellationToken ct)
    {
        if (command.Draft is null) throw Invalid("Product", "A product draft is required.", 400);
        var hash = Hash(command);
        var draft = ProductEditorRules.Copy(command.Draft);
        await ProcessInlineBase64ImagesAsync(draft, ct);
        ValidateInformation(draft);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockOperationAsync(actor, command.OperationId, ct);
        if (await ReceiptAsync(actor, command.OperationId, hash, ct) is { } previous)
        {
            await transaction.CommitAsync(ct);
            return await CompletedAsync(JsonSerializer.Deserialize<EditorReceipt>(previous.ResultJson, Json)!, ct);
        }
        if (draft.Id > 0)
        {
            await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"Products\" WHERE \"Id\" = {draft.Id} FOR UPDATE", ct);
            var current = await db.Products.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Id == draft.Id, ct) ?? throw new ProductEditorException(404, "Product not found.");
            if (current.CatalogRevision != draft.Revision) throw Invalid("Revision", "Another editor changed this product. Keep your draft and review the latest version before saving.", 409);
        }
        var id = await admin.SaveProductAsync(ToForm(draft), unifiedEditor: true);
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == id, ct);
        var options = await SaveAttributesAsync(id, draft.Attributes, command.OperationId, ct);
        await SaveVariantsAsync(id, draft, options, command.OperationId, actor, ct);
        if (!draft.Information.Hidden && !await db.ProductVariants.AnyAsync(v => v.ProductId == id && !v.IsDeleted && v.IsActive && v.IsDefault && v.SellingPrice > 0, ct))
            throw Invalid("Publish", "Publishing needs exactly one active default variant with a positive price.");
        Record(actor, command.OperationId, id, product.CatalogRevision, "product", hash);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await CompletedAsync(new(command.OperationId, id, product.CatalogRevision), ct);
    }
    private async Task ProcessInlineBase64ImagesAsync(ProductEditorDraft draft, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(draft.Information.Details)) return;
        var html = draft.Information.Details;
        var matches = Regex.Matches(html, @"data:image/(?<type>[a-zA-Z0-9+]+);base64,(?<data>[A-Za-z0-9+/=]+)");
        if (matches.Count == 0) return;

        var sb = new StringBuilder();
        int lastIndex = 0;
        foreach (Match match in matches)
        {
            sb.Append(html, lastIndex, match.Index - lastIndex);
            lastIndex = match.Index + match.Length;

            try
            {
                var type = match.Groups["type"].Value.ToLowerInvariant();
                var base64 = match.Groups["data"].Value;
                var bytes = Convert.FromBase64String(base64);
                var ext = type switch
                {
                    "jpeg" or "jpg" => ".jpg",
                    "png" => ".png",
                    "webp" => ".webp",
                    "avif" => ".avif",
                    "gif" => ".gif",
                    _ => ".png"
                };
                var contentType = type switch
                {
                    "jpeg" or "jpg" => "image/jpeg",
                    "png" => "image/png",
                    "webp" => "image/webp",
                    "avif" => "image/avif",
                    "gif" => "image/gif",
                    _ => "image/png"
                };
                using var stream = new MemoryStream(bytes);
                var fileName = $"spec-{Guid.NewGuid():N}{ext}";
                var url = await ImageUploadValidator.UploadAsync(storage, stream, fileName, contentType, ct);
                sb.Append(url);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to upload inline base64 image from product details to R2.");
                sb.Append(match.Value);
            }
        }
        sb.Append(html, lastIndex, html.Length - lastIndex);
        draft.Information.Details = sb.ToString();
    }
    private static void ValidateInformation(ProductEditorDraft draft)
    {
        if (draft.Information is null || draft.Attributes is null || draft.Variants is null) throw Invalid("Product", "Invalid product draft.", 400);
        var form = ToForm(draft);
        List<ValidationResult> errors = [];
        Validator.TryValidateObject(form, new(form), errors, true);
        var actionable = errors.Where(e => !(form.Hidden && e.MemberNames.All(name => name is nameof(form.Image) or nameof(form.Details)))).ToList();
        if (actionable.Count > 0) throw new ProductEditorException(422, "Review the highlighted product fields.", actionable.SelectMany(e => e.MemberNames.Select(name => (name, e.ErrorMessage!))).GroupBy(e => e.name).ToDictionary(g => g.Key, g => g.Select(e => e.Item2).ToArray()));
        if (draft.Attributes.Count > 50 || draft.Attributes.Any(a => a is null) || draft.Variants.Any(v => v is null)) throw Invalid("Options", "Use at most 50 attributes.");
    }
    private static ProductEditForm ToForm(ProductEditorDraft d) => new() { Id = d.Id, CatalogRevision = d.Revision,
        Name = d.Information.Name ?? "", Slug = d.Information.Slug, CategoryId = d.Information.CategoryId, BrandId = d.Information.BrandId,
        Description = d.Information.Description ?? "", Details = d.Information.Details ?? "", Image = d.Information.Image ?? "", Images = d.Information.Images ?? "",
        Hidden = d.Information.Hidden, IsFeatured = d.Information.Featured, IsNewArrival = d.Information.NewArrival, MetaTitle = d.Information.MetaTitle,
        MetaDescription = d.Information.MetaDescription, OgImageUrl = d.Information.OgImageUrl, Synonyms = d.Information.Synonyms, BanglaName = d.Information.BanglaName,
        BanglaDescription = d.Information.BanglaDescription, WarrantyInfo = d.Information.WarrantyInfo };
    public async Task<EditorCommit> StockAsync(int actor, EditorStock command, CancellationToken ct)
    {
        if (command.Quantity < 0 || string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500) throw Invalid("Stock", "Enter a non-negative quantity and a reason of 1–500 characters.");
        var hash = Hash(command);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockOperationAsync(actor, command.OperationId, ct);
        if (await ReceiptAsync(actor, command.OperationId, hash, ct) is { } previous)
        { await transaction.CommitAsync(ct); return await CompletedAsync(JsonSerializer.Deserialize<EditorReceipt>(previous.ResultJson, Json)!, ct); }
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"Products\" WHERE \"Id\" = {command.ProductId} FOR UPDATE", ct);
        var variant = await db.ProductVariants.SingleOrDefaultAsync(v => v.Id == command.VariantId && v.ProductId == command.ProductId && !v.IsDeleted, ct) ?? throw new ProductEditorException(404, "Variant not found.");
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {variant.Id} FOR UPDATE", ct);
        var stock = await db.InventoryItems.SingleOrDefaultAsync(i => i.ProductVariantId == variant.Id, ct);
        if ((stock?.Version ?? 0) != command.ExpectedVersion) throw Invalid("Stock", "Inventory changed. Your quantity and reason are retained; review current stock before retrying.", 409);
        if (command.Quantity < (stock?.QuantityReserved ?? 0)) throw Invalid("Stock", "On-hand stock cannot be below current reservations.", 409);
        stock ??= new InventoryItem { ProductVariantId = variant.Id };
        if (stock.Id == 0) db.InventoryItems.Add(stock);
        var delta = command.Quantity - stock.QuantityOnHand;
        stock.QuantityOnHand = command.Quantity;
        if (delta != 0) db.InventoryTransactions.Add(new() { ProductVariantId = variant.Id, QuantityChange = delta, Type = InventoryTransactionType.Adjustment, ReferenceType = "AdminStock", Note = command.Reason.Trim(), CreatedByUserId = actor });
        var revision = await db.Products.Where(p => p.Id == command.ProductId).Select(p => p.CatalogRevision).SingleAsync(ct);
        Record(actor, command.OperationId, command.ProductId, revision, "stock", hash);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return await CompletedAsync(new(command.OperationId, command.ProductId, revision), ct);
    }
}
