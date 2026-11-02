using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Services;

public record CouponValidationResult(
    bool IsValid,
    string? Code,
    decimal DiscountAmount,
    string? Message
);

public sealed class CouponService(AppDbContext db)
{
    public async Task<CouponValidationResult> ValidateCouponAsync(string code, decimal subtotal, decimal deliveryCharge = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new CouponValidationResult(false, null, 0, "Please enter a coupon code.");
        }

        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized && c.IsActive);

        if (coupon == null)
        {
            return new CouponValidationResult(false, normalized, 0, "Invalid coupon code.");
        }

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTimeOffset.UtcNow)
        {
            return new CouponValidationResult(false, normalized, 0, "This coupon has expired.");
        }

        if (coupon.UsageLimit.HasValue && coupon.TimesUsed >= coupon.UsageLimit.Value)
        {
            return new CouponValidationResult(false, normalized, 0, "This coupon's usage limit has been reached.");
        }

        if (coupon.MinOrderAmount.HasValue && subtotal < coupon.MinOrderAmount.Value)
        {
            return new CouponValidationResult(false, normalized, 0, $"Order subtotal must be at least ৳{coupon.MinOrderAmount.Value:N0} to use this coupon.");
        }

        decimal discount = 0;
        switch (coupon.DiscountType)
        {
            case CouponDiscountType.Percentage:
                discount = Math.Round(subtotal * (coupon.DiscountValue / 100m), 2);
                if (coupon.MaxDiscountAmount.HasValue && discount > coupon.MaxDiscountAmount.Value)
                {
                    discount = coupon.MaxDiscountAmount.Value;
                }
                break;

            case CouponDiscountType.FixedAmount:
                discount = Math.Min(coupon.DiscountValue, subtotal);
                break;

            case CouponDiscountType.FreeDelivery:
                discount = deliveryCharge;
                break;
        }

        return new CouponValidationResult(true, coupon.Code, discount, $"Coupon '{coupon.Code}' applied successfully!");
    }

    public async Task RecordCouponUseAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == normalized);
        if (coupon != null)
        {
            coupon.TimesUsed++;
            await db.SaveChangesAsync();
        }
    }
}
