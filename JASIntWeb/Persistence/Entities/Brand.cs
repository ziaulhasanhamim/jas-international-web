namespace JASIntWeb.Persistence.Entities;

public sealed class Brand : SoftDeletableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImageUrl { get; set; }
    public string? IntroText { get; set; }
    public string? BottomSeoCopy { get; set; }
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public ICollection<Product> Products { get; init; } = [];
}
