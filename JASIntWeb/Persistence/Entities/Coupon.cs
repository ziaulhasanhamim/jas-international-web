namespace JASIntWeb.Persistence.Entities;

public enum CouponDiscountType
{
    Percentage,
    FixedAmount,
    FreeDelivery
}

public class Coupon : AuditableEntity
{
    public required string Code { get; set; }
    public CouponDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public int? UsageLimit { get; set; }
    public int TimesUsed { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public List<int> IncludedCategoryIds { get; set; } = [];
    public List<int> ExcludedCategoryIds { get; set; } = [];
    public List<int> IncludedBrandIds { get; set; } = [];
    public List<int> ExcludedBrandIds { get; set; } = [];
    public List<int> IncludedTagIds { get; set; } = [];
    public List<int> ExcludedTagIds { get; set; } = [];

    public bool HasTargetingRules =>
        IncludedCategoryIds.Count > 0 ||
        ExcludedCategoryIds.Count > 0 ||
        IncludedBrandIds.Count > 0 ||
        ExcludedBrandIds.Count > 0 ||
        IncludedTagIds.Count > 0 ||
        ExcludedTagIds.Count > 0;
}
