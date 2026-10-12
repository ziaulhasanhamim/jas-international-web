namespace JASIntWeb.Persistence.Entities;

public sealed class ProductAttributeDefinition : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition? AttributeDefinition { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public ICollection<ProductAttributeOption> Options { get; set; } = [];
}

public sealed class ProductAttributeOption : Entity
{
    public int ProductAttributeDefinitionId { get; set; }
    public ProductAttributeDefinition? ProductAttributeDefinition { get; set; }
    // These key parts enforce product/definition ownership through composite foreign keys.
    public int ProductId { get; set; }
    public int AttributeDefinitionId { get; set; }
    public int AttributeOptionId { get; set; }
    public AttributeOption? AttributeOption { get; set; }
    public int SortOrder { get; set; }
}
