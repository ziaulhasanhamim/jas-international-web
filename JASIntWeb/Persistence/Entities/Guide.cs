namespace JASIntWeb.Persistence.Entities;

public sealed class Guide : SoftDeletableEntity
{
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Summary { get; set; }
    public required string ContentHtml { get; set; }
    public string? BanglaTitle { get; set; }
    public string? BanglaSummary { get; set; }
    public string? BanglaContentHtml { get; set; }
    public string Author { get; set; } = "JAS Editorial Team";
    public string? FeaturedImageUrl { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
}

