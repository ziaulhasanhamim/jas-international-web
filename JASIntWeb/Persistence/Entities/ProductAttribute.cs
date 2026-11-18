namespace JASIntWeb.Persistence.Entities;

public sealed class ProductAttributeDefinition : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition? AttributeDefinition { get; set; }
    public int SortOrder { get; set; }
    public string MetadataSchemaJson { get; set; } = "[]";
    public string? SwatchFieldKey { get; set; }
    public bool IsRetired { get; set; }
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
    public int? AttributeOptionId { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public bool IsRetired { get; set; }
    public string Value { get; set; } = "";
    public string Code { get; set; } = "";
    public string NormalizedValue { get; set; } = "";
    public AttributeOption? AttributeOption { get; set; }
    public int SortOrder { get; set; }
}
