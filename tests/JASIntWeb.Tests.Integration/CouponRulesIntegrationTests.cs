using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class CouponRulesIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task ValidateCouponAsync_WithIncludeCategoryAndExcludeBrand_CorrectlyApplies()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var couponSvc = scope.ServiceProvider.GetRequiredService<CouponService>();

        // 1. Create Category "Earbuds"
        var category = new Category { Name = "Earbuds Integration", Slug = "earbuds-integration" };
        db.Categories.Add(category);

        // 2. Create Brands "Ulanzi" and "Anker"
        var ulanzi = new Brand { Name = "Ulanzi", Slug = "ulanzi" };
        var anker = new Brand { Name = "Anker Audio", Slug = "anker-audio" };
        db.Brands.AddRange(ulanzi, anker);
        await db.SaveChangesAsync();

        // 3. Create Products
        var prodA = new Product { Name = "Ulanzi Earbuds", Slug = "ulanzi-earbuds", Details = "", CategoryId = category.Id, BrandId = ulanzi.Id, MainImageUrl = "" };
        var varA = new ProductVariant { SKU = "ULZ-EB-01", SellingPrice = 1000m, IsDefault = true, Product = prodA };
        prodA.ProductVariants.Add(varA);

        var prodB = new Product { Name = "Anker Soundcore", Slug = "anker-soundcore", Details = "", CategoryId = category.Id, BrandId = anker.Id, MainImageUrl = "" };
        var varB = new ProductVariant { SKU = "ANK-SC-01", SellingPrice = 2000m, IsDefault = true, Product = prodB };
        prodB.ProductVariants.Add(varB);

        db.Products.AddRange(prodA, prodB);

        // 4. Create Targeting Coupon:
        // Applies to all "Earbuds" except brand "Ulanzi" -> 20% discount
        var coupon = new Coupon
        {
            Code = "EARBUDS20",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 20,
            IncludedCategoryIds = [category.Id],
            ExcludedBrandIds = [ulanzi.Id],
            IsActive = true
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();

        // Test Case 1: Cart contains only Ulanzi earbuds -> Should reject!
        var ulanziCart = new List<CartLineRequest> { new(varA.Id, 1) };
        var resUlanzi = await couponSvc.ValidateCouponAsync("EARBUDS20", 1000m, 100m, ulanziCart);
        await resUlanzi.IsValid.Should().BeFalse();
        await resUlanzi.Message!.Contains("not valid for any items in your cart").Should().BeTrue();

        // Test Case 2: Cart contains Anker earbuds -> Should apply 20% on 2000 = 400!
        var ankerCart = new List<CartLineRequest> { new(varB.Id, 1) };
        var resAnker = await couponSvc.ValidateCouponAsync("EARBUDS20", 2000m, 100m, ankerCart);
        await resAnker.IsValid.Should().BeTrue();
        await resAnker.DiscountAmount.Should().BeEqualTo(400m);

        // Test Case 3: Mixed cart (Ulanzi 1000 + Anker 2000 = subtotal 3000)
        // Discount should be 20% of 2000 = 400 (NOT 20% of 3000 = 600)
        var mixedCart = new List<CartLineRequest> { new(varA.Id, 1), new(varB.Id, 1) };
        var resMixed = await couponSvc.ValidateCouponAsync("EARBUDS20", 3000m, 100m, mixedCart);
        await resMixed.IsValid.Should().BeTrue();
        await resMixed.DiscountAmount.Should().BeEqualTo(400m);
    }
}
