namespace JASIntWeb.Persistence.Entities;

public sealed class ProductEditorReceipt
{
    public int ActorId { get; set; }
    public Guid OperationId { get; set; }
    public int ProductId { get; set; }
    public string CommandKind { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
