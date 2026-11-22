using JASIntWeb.Helpers;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Services;

public sealed record AppliedCoupon(
    string Code,
    decimal DiscountAmount,
    CouponDiscountType DiscountType,
    string Message
);

public record CouponValidationResult(
    bool IsValid,
    string? Code,
    decimal DiscountAmount,
    string? Message,
    CouponDiscountType? DiscountType = null
);

public sealed class CouponService(AppDbContext db)
{
    public async Task<Result<AppliedCoupon>> ApplyCouponAsync(string code, decimal subtotal, decimal deliveryCharge = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.Validation("Please enter a coupon code.");
        }

        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized && c.IsActive);

        if (coupon == null)
        {
            return Error.NotFound("Invalid coupon code.");
        }

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTimeOffset.UtcNow)
        {
            return Error.Validation("This coupon has expired.");
        }

        if (coupon.UsageLimit.HasValue && coupon.TimesUsed >= coupon.UsageLimit.Value)
        {
            return Error.Validation("This coupon's usage limit has been reached.");
        }

        if (coupon.MinOrderAmount.HasValue && subtotal < coupon.MinOrderAmount.Value)
        {
            return Error.Validation($"Order subtotal must be at least ৳{coupon.MinOrderAmount.Value:N0} to use this coupon.");
        }

        decimal discount = coupon.DiscountType switch
        {
            CouponDiscountType.Percentage => coupon.MaxDiscountAmount.HasValue
                ? Math.Min(Math.Round(subtotal * (coupon.DiscountValue / 100m), 2), coupon.MaxDiscountAmount.Value)
                : Math.Round(subtotal * (coupon.DiscountValue / 100m), 2),
            CouponDiscountType.FixedAmount => Math.Min(coupon.DiscountValue, subtotal),
            CouponDiscountType.FreeDelivery => deliveryCharge,
            _ => 0m
        };

        return new AppliedCoupon(coupon.Code, discount, coupon.DiscountType, $"Coupon '{coupon.Code}' applied successfully!");
    }

    public async Task<CouponValidationResult> ValidateCouponAsync(string code, decimal subtotal, decimal deliveryCharge = 0)
    {
        var result = await ApplyCouponAsync(code, subtotal, deliveryCharge);
        return result switch
        {
            AppliedCoupon c => new CouponValidationResult(true, c.Code, c.DiscountAmount, c.Message, c.DiscountType),
            Error err => new CouponValidationResult(false, code.Trim().ToUpperInvariant(), 0, err.Message)
        };
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
