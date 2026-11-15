namespace JASIntWeb.Persistence.Entities;

public sealed class Category : SoftDeletableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Icon { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImageUrl { get; set; }
    public string? IntroText { get; set; }
    public string? BottomSeoCopy { get; set; }
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> ChildCategories { get; init; } = [];
    public ICollection<Product> Products { get; init; } = [];
}
