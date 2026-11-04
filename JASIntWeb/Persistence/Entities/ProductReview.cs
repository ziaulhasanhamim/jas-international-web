namespace JASIntWeb.Persistence.Entities;

public sealed class ProductReview : AuditableEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public required string CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public int Rating { get; set; } = 5;
    public string? Title { get; set; }
    public required string Comment { get; set; }
    public bool IsApproved { get; set; } = true;
    public string? StaffReply { get; set; }
}
