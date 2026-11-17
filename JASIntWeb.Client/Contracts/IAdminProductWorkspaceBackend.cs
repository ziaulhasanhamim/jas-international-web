namespace JASIntWeb.Client.Contracts;

public interface IAdminProductWorkspaceBackend
{
    Task<ProductWorkspace> LoadAsync(int productId);
    Task<ProductWorkspace> SaveConfigurationAsync(int productId, ProductConfiguration command);
    Task<ProductWorkspace> SaveVariantAsync(int productId, WorkspaceVariant command);
    Task<ProductWorkspace> AdjustStockAsync(int productId, WorkspaceStock command);
    Task<CombinationPreview> PreviewAsync(int productId, long revision);
    Task<ProductWorkspace> GenerateAsync(int productId, long revision);
}

public sealed record AttributeSuggestion(int Id, string Name, List<ValueSuggestion> Values);
public sealed record ValueSuggestion(int Id, string Value);
public sealed record ProductWorkspace(int ProductId, string Name, long Revision,
    List<WorkspaceAttribute> Attributes, List<WorkspaceVariant> Variants, List<AttributeSuggestion> Suggestions);
public sealed class WorkspaceAttribute
{
    public WorkspaceAttribute Copy() => new() { DefinitionId = DefinitionId, Name = Name, Required = Required, Options = Options.Select(option => option.Copy()).ToList() };
    public int DefinitionId { get; set; }
    public string Name { get; set; } = "";
    public bool Required { get; set; } = true;
    public List<WorkspaceOption> Options { get; set; } = [];
}
public sealed class WorkspaceOption
{
    public int Id { get; set; }
    public WorkspaceOption Copy() => (WorkspaceOption)MemberwiseClone();
    public int? SuggestionId { get; set; }
    public string Value { get; set; } = "";
    public string Code { get; set; } = "";
}
public sealed class WorkspaceVariant
{
    public int Id { get; set; }
    public WorkspaceVariant Copy() { var copy = (WorkspaceVariant)MemberwiseClone(); copy.Choices = new(Choices); return copy; }
    public long Revision { get; set; }
    public string Sku { get; set; } = "";
    public string? Barcode { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool Active { get; set; }
    public bool Default { get; set; }
    public bool Archived { get; set; }
    public int OnHand { get; set; }
    public int Reserved { get; set; }
    public Dictionary<int, int> Choices { get; set; } = [];
}
public sealed record ProductConfiguration(long Revision, List<WorkspaceAttribute> Attributes, List<WorkspaceVariant> Mappings);
public sealed record WorkspaceStock(long Revision, int VariantId, int ExpectedOnHand, int Quantity, string Reason);
public sealed record CombinationPreview(int Total, int Missing);
public sealed record WorkspaceRevision(long Revision);
public sealed record ContentPreviewRequest(string Source);
public sealed record ContentPreview(string Html);
