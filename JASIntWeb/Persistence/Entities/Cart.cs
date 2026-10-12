namespace JASIntWeb.Persistence.Entities;

public class Cart : AuditableEntity
{
    public Guid Token { get; set; } = Guid.CreateVersion7();
    public int? UserId { get; set; }
    public User? User { get; set; }
    public CartStatus Status { get; set; } = CartStatus.Active;
    public ICollection<CartItem> Items { get; set; } = [];
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class CartItem : AuditableEntity
{
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public required int ProductVariantId { get; init; }
    public ProductVariant? ProductVariant { get; init; }
    public int Quantity { get; set; }
}

public enum CartStatus
{
    Active,
    Converted,
    Abandoned,
    Expired
}
