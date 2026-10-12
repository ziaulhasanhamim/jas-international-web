namespace JASIntWeb.Persistence.Entities;

public enum OrderStatus
{
    Pending,
    Processing,
    InTransit,
    Delivered,
    Cancelled
}

public class Order : AuditableEntity
{
    public Guid PublicToken { get; set; } = Guid.NewGuid();
    public Guid CheckoutToken { get; set; } = Guid.NewGuid();
    public int? UserId { get; set; }
    public User? User { get; set; }
    public required OrderStatus Status { get; set; }
    public required int DeliveryAddressId { get; set; }
    public DeliveryAddress? DeliveryAddress { get; set; }
    public required decimal Subtotal { get; init; }
    public required decimal DeliveryCharge { get; init; }
    public required decimal Total { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}

public class OrderItem : AuditableEntity
{
    public int OrderId { get; init; }
    public Order? Order { get; init; }
    public int ProductVariantId { get; init; }
    public ProductVariant? ProductVariant { get; init; }
    public required string ProductName { get; init; }
    public required string VariantName { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public enum PaymentMethod
{
    Card,
    Bkash,
    CashOnDelivery
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}

public class Payment : AuditableEntity
{
    public required int OrderId { get; init; }
    public Order? Order { get; init; }
    public decimal Amount { get; init; }
    public required PaymentMethod Method { get; init; }
    public required PaymentStatus Status { get; set; }
    public string? ExternalReference { get; set; }
}
