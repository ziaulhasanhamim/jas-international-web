namespace JASIntWeb.Persistence.Entities;

public class ProductVariant : SoftDeletableEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public required string SKU { get; set; }
    public string OptionSignature { get; set; } = "";
    public string? Barcode { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public ICollection<ProductVariantOption> Options { get; set; } = [];
}

public class ProductVariantOption : Entity
{
    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public int ProductId { get; set; }
    public int ProductAttributeDefinitionId { get; set; }
    public int ProductAttributeOptionId { get; set; }
    public ProductAttributeOption? ProductAttributeOption { get; set; }
}
