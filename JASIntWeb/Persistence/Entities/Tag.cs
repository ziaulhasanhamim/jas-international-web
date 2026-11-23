namespace JASIntWeb.Persistence.Entities;

public sealed class Tag : SoftDeletableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public bool IsHidden { get; set; }
    public string? BadgeColor { get; set; } // e.g. "emerald", "amber", "rose", "blue", "indigo", "purple"
    public int SortOrder { get; set; }
    public ICollection<ProductTag> ProductTags { get; init; } = [];
}
