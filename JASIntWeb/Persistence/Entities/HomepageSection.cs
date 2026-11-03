namespace JASIntWeb.Persistence.Entities;

public sealed class HomepageSection : Entity
{
    public string Title { get; set; } = string.Empty;
    public string? Eyebrow { get; set; }
    public string? CategorySlug { get; set; }
    public string ViewAllHref { get; set; } = "/products";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public int ItemLimit { get; set; } = 8;
}
