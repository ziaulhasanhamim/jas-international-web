using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;

namespace JASIntWeb.Client.Contracts;

public interface IAdminProductEditorBackend
{
    Task<Result<EditorTemplateData>> TemplateAsync(int id, CancellationToken ct = default);
    Task<Result<EditorTemplateData>> SaveTemplateAsync(EditorTemplateData command, CancellationToken ct = default);
    Task<Result<EditorSuggestionData>> SuggestionAsync(int id, CancellationToken ct = default);
    Task<Result<EditorSuggestionData>> SaveSuggestionAsync(EditorSuggestionData command, CancellationToken ct = default);
    Task<Result<ProductEditorData>> LoadAsync(int id, CancellationToken ct = default);
    Task<Result<EditorCombinationPage>> PreviewAsync(EditorCombinationQuery query, CancellationToken ct = default);
    Task<Result<EditorCommit>> SaveAsync(EditorSave command, CancellationToken ct = default);
    Task<Result<EditorCommit>> AdjustStockAsync(EditorStock command, CancellationToken ct = default);
    Task<Result<EditorCommit?>> OperationAsync(Guid operationId, CancellationToken ct = default);
}
public enum ValueFieldKind { Text, Number, Boolean, HexColor }
public sealed record ValueField(string Key, string Label, ValueFieldKind Kind);
public sealed class EditorOption
{
    public Guid ClientId { get; set; } = Guid.NewGuid();
    public int Id { get; set; }
    public string Value { get; set; } = "";
    public string Code { get; set; } = "";
    public int? SuggestionId { get; set; }
    public bool Retired { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
public sealed class EditorAttribute
{
    public int DefinitionId { get; set; }
    public string Name { get; set; } = "";
    public bool Required { get; set; } = true;
    public bool Retired { get; set; }
    public List<ValueField> Fields { get; set; } = [];
    public string? SwatchField { get; set; }
    public List<EditorOption> Options { get; set; } = [];
}
public sealed class EditorVariant
{
    public Guid ClientId { get; set; } = Guid.NewGuid();
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public decimal? Cost { get; set; }
    public decimal? CompareAt { get; set; }
    public bool Active { get; set; }
    public bool Default { get; set; }
    public bool Archived { get; set; }
    public int OpeningStock { get; set; }
    public int OnHand { get; set; }
    public int Reserved { get; set; }
    public uint InventoryVersion { get; set; }
    public Dictionary<int, Guid> Choices { get; set; } = [];
}
public sealed class ProductInformation
{
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public string Description { get; set; } = "";
    public string Details { get; set; } = "";
    public string Image { get; set; } = "";
    public string Images { get; set; } = "";
    public bool Hidden { get; set; } = true;
    public bool Featured { get; set; }
    public bool NewArrival { get; set; } = true;
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImageUrl { get; set; }
    public string? Synonyms { get; set; }
    public string? BanglaName { get; set; }
    public string? BanglaDescription { get; set; }
    public string? WarrantyInfo { get; set; } = "1 Year Official Brand Warranty";
    public List<int> TagIds { get; set; } = [];
}
public sealed class ProductEditorDraft
{
    public int Id { get; set; }
    public long Revision { get; set; }
    public ProductInformation Information { get; set; } = new();
    public List<EditorAttribute> Attributes { get; set; } = [];
    public List<EditorVariant> Variants { get; set; } = [];
}
public sealed record EditorAttributeTemplate(int Id, string Name, List<ValueField> Fields, string? SwatchField, List<EditorOption> Suggestions);
public sealed record ProductEditorData(ProductEditorDraft Draft, List<CategoryLinkDto> Categories, List<BrandLinkDto> Brands,
    List<EditorAttributeTemplate> Templates, string PublicBaseUrl, List<TagDto>? Tags = null);
public sealed record EditorSave(Guid OperationId, ProductEditorDraft Draft);
public sealed record EditorStock(Guid OperationId, int ProductId, int VariantId, uint ExpectedVersion, int Quantity, string Reason);
public sealed record EditorReceipt(Guid OperationId, int ProductId, long Revision);
public sealed record EditorCommit(EditorReceipt Receipt, ProductEditorData? Editor, string? RefreshWarning = null);
public sealed record EditorCombinationQuery(ProductEditorDraft Draft, string Offset = "0", Dictionary<int, List<Guid>>? Filters = null);
public sealed record EditorCombination(string Key, Dictionary<int, Guid> Choices, string Label, string Status, List<int> ExistingIds);
public sealed record EditorCombinationPage(string Total, string Offset, bool HasMore, List<EditorCombination> Rows);

public sealed record EditorTemplateData(int Id, List<ValueField> Fields, string? SwatchField);
public sealed record EditorSuggestionData(int Id, List<ValueField> Fields, Dictionary<string, string> Values);
