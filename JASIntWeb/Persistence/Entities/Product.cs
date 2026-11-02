namespace JASIntWeb.Persistence.Entities;

public sealed class Product : SoftDeletableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Details { get; set; }
    public string? Description { get; set; }
    public required string MainImageUrl { get; set; }
    public required int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public ICollection<ProductAttributeDefinition> Attributes { get; set; } = [];
    public ICollection<ProductImage> ProductImages { get; init; } = [];
    public ICollection<ProductVariant> ProductVariants { get; init; } = [];
}

public sealed class ProductImage
{
    public int Id { get; init; }
    public required string ImageUrl { get; set; }
    public required int ProductId { get; init; }
    public int SortOrder { get; set; }
    public Product? Product { get; init; }
}
