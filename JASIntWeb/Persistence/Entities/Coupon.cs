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
}
