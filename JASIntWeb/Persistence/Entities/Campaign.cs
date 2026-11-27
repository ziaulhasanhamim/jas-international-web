using JASIntWeb.Client.Dtos;

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
    public string? BadgeText { get; set; }
    public CampaignDiscountScenario DiscountScenario { get; set; } = CampaignDiscountScenario.MarketingOnly;
    public CampaignProductDiscountType? DefaultDiscountType { get; set; }
    public decimal? DefaultDiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public string? HighlightCouponCode { get; set; }
    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }
    public int? TagId { get; set; }
    public Tag? Tag { get; set; }
    public List<int> CuratedProductIds { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImageUrl { get; set; }
    public ICollection<CampaignProduct> CampaignProducts { get; init; } = [];
}

public sealed class CampaignProduct : Entity
{
    public int CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public CampaignProductDiscountType DiscountType { get; set; } = CampaignProductDiscountType.Percentage;
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int SortOrder { get; set; }
}
