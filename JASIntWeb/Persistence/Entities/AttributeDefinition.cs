namespace JASIntWeb.Persistence.Entities;

public class AttributeDefinition : Entity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Unit { get; set; }
    public ICollection<AttributeOption> Options { get; set; } = [];
}

public class AttributeOption : Entity
{
    public int AttributeDefinitionId { get; set; }
    public required string Value { get; set; }
    public required string Code { get; set; }
    public AttributeDefinition? AttributeDefinition { get; set; }
}
