namespace JASIntWeb.Persistence.Entities;

public enum CategoryPlacementType
{
    HomepageFeatured
}

public sealed class CategoryPlacement : Entity
{
    public int CategoryId { get; init; }
    public Category? Category { get; set; }
    public CategoryPlacementType PlacementType { get; init; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
