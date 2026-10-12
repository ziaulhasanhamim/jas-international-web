namespace JASIntWeb.Persistence.Entities;

public enum InventoryTransactionType
{
    Purchase,
    Sale,
    CustomerReturn,
    SupplierReturn,
    Adjustment
}

public class InventoryItem : Entity
{
    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public int QuantityOnHand { get; set; }
    // Items allocated to pending/confirmed orders
    public int QuantityReserved { get; set; }
    public int AvailableQuantity =>
    QuantityOnHand - QuantityReserved;
}

public class InventoryTransaction : AuditableEntity
{
    public int ProductVariantId { get; init; }
    public ProductVariant? ProductVariant { get; init; }
    public int QuantityChange { get; init; }
    public InventoryTransactionType Type { get; init; }
    public string? ReferenceType { get; init; }
    public int? ReferenceId { get; init; }
    public string? Note { get; set; }
}
