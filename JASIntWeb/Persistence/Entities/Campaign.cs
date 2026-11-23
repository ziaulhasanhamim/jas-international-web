namespace JASIntWeb.Persistence.Entities;

public sealed class Campaign : AuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? BanglaName { get; set; }
    public string? Eyebrow { get; set; }
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? MobileBannerImageUrl { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public bool ShowCountdown { get; set; } = true;
    public string ThemeColor { get; set; } = "indigo"; // indigo, crimson, amber, emerald, navy
    public string? HighlightCouponCode { get; set; }
    public int? TagId { get; set; }
    public Tag? Tag { get; set; }
    public List<int> CuratedProductIds { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImageUrl { get; set; }
}
