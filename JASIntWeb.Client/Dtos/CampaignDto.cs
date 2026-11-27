using System.ComponentModel.DataAnnotations;

namespace JASIntWeb.Client.Dtos;

public sealed record CampaignSummaryDto(
    int Id,
    string Name,
    string Slug,
    string? BanglaName,
    string? Eyebrow,
    string? Description,
    string? BannerImageUrl,
    string? MobileBannerImageUrl,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    bool ShowCountdown,
    string ThemeColor,
    string? HighlightCouponCode,
    int ProductCount,
    bool IsActive,
    int SortOrder = 0);

public sealed record CampaignDetailsDto(
    int Id,
    string Name,
    string Slug,
    string? BanglaName,
    string? Eyebrow,
    string? Description,
    string? BannerImageUrl,
    string? MobileBannerImageUrl,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    bool ShowCountdown,
    string ThemeColor,
    string? HighlightCouponCode,
    string? MetaTitle,
    string? MetaDescription,
    string? OgImageUrl,
    List<ProductSummaryDto> Products);

public sealed class CampaignFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Campaign Name is required."), StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
    public string Name { get; set; } = "";

    [StringLength(150, ErrorMessage = "Slug cannot exceed 150 characters.")]
    public string? Slug { get; set; }

    [StringLength(150, ErrorMessage = "Bangla name cannot exceed 150 characters.")]
    public string? BanglaName { get; set; }

    [StringLength(100, ErrorMessage = "Eyebrow cannot exceed 100 characters.")]
    public string? Eyebrow { get; set; } = "MEGA FESTIVAL SALE · LIMITED TIME";

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "Banner URL cannot exceed 500 characters.")]
    public string? BannerImageUrl { get; set; }

    [StringLength(500, ErrorMessage = "Mobile Banner URL cannot exceed 500 characters.")]
    public string? MobileBannerImageUrl { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool ShowCountdown { get; set; } = true;

    [StringLength(50)]
    public string ThemeColor { get; set; } = "indigo"; // indigo, crimson, emerald, amber, navy, purple

    [StringLength(50)]
    public string? HighlightCouponCode { get; set; }

    public int? CollectionId { get; set; }

    public int? TagId { get; set; }

    public List<int> CuratedProductIds { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    [StringLength(200)]
    public string? MetaTitle { get; set; }

    [StringLength(300)]
    public string? MetaDescription { get; set; }

    [StringLength(500)]
    public string? OgImageUrl { get; set; }
}
