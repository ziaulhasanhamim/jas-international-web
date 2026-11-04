using System.ComponentModel.DataAnnotations;
namespace JASIntWeb.Client.Dtos;

public sealed class ProductEditForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Product name is required"), StringLength(200)]
    public string Name { get; set; } = "";
    [StringLength(120)]
    public string? Slug { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Please select a category")]
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    [StringLength(2000)]
    public string Description { get; set; } = "";
    [Required(ErrorMessage = "Product description details are required"), StringLength(100000)]
    public string Details { get; set; } = "";
    [Required(ErrorMessage = "Main product picture is required"), StringLength(5000)]
    public string Image { get; set; } = "";
    [StringLength(50000)]
    public string Images { get; set; } = "";
    public bool Hidden { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNewArrival { get; set; } = true;
    public decimal? CompareAtPrice { get; set; }

    // Quick Simple Product Fields (for effortless single-screen creation)
    [Range(0, 10000000)]
    public decimal? BasePrice { get; set; }
    [Range(0, 10000000)]
    public decimal? CostPrice { get; set; }
    [Range(0, 100000)]
    public int? InitialStock { get; set; }
    [StringLength(100)]
    public string? Sku { get; set; }
    [StringLength(100)]
    public string? Barcode { get; set; }
    public bool HasVariants { get; set; }
}

public sealed class VariantEditForm
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    [Required, StringLength(100)]
    public string Sku { get; set; } = "";
    [Range(0, 10000000)]
    public decimal SellingPrice { get; set; }
    [Range(0, 10000000)]
    public decimal CostPrice { get; set; }
    [Range(0, int.MaxValue)]
    public int Stock { get; set; }
    public bool Active { get; set; } = true;
    public bool Default { get; set; }
    public List<VariantChoiceForm> Choices { get; set; } = [];
}

public sealed class VariantChoiceForm
{
    public int ProductAttributeDefinitionId { get; set; }
    public int ProductAttributeOptionId { get; set; }
}

public sealed class CategoryEditForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Category name is required"), StringLength(200)]
    public string Name { get; set; } = "";
    [StringLength(120)]
    public string? Slug { get; set; } = "";
    [StringLength(2000)]
    public string Icon { get; set; } = "";
    public int? ParentId { get; set; }
    public bool Hidden { get; set; }
    public bool Featured { get; set; }
}

public sealed class AttributeEditForm
{
    public int Id { get; set; }
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    [Required, StringLength(120)]
    public string Code { get; set; } = "";
    [StringLength(30)]
    public string? Unit { get; set; }
}

public sealed class OptionEditForm
{
    public int Id { get; set; }
    public int AttributeDefinitionId { get; set; }
    [Required, StringLength(100)]
    public string Value { get; set; } = "";
    [Required, StringLength(120)]
    public string Code { get; set; } = "";
}

public sealed class SlideEditForm
{
    public int Id { get; set; }
    [StringLength(200)]
    public string? Title { get; set; }
    [StringLength(100)]
    public string? Eyebrow { get; set; }
    [StringLength(600)]
    public string? Copy { get; set; }
    [Required, StringLength(2000)]
    public string Image { get; set; } = "";
    [Required, StringLength(200)]
    public string Alt { get; set; } = "";
    [StringLength(2000)]
    public string? Link { get; set; }
    [StringLength(100)]
    public string? LinkText { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class OrderEditForm
{
    public int Id { get; set; }
    [Required]
    public string Status { get; set; } = "Pending";
    public string? CourierName { get; set; }
    public string? CourierTrackingId { get; set; }
    public string? CourierTrackingUrl { get; set; }
    public string? ConsignmentId { get; set; }
    public decimal CodAmountToCollect { get; set; }
    public string? AdminNotes { get; set; }
}

public sealed class ChangeStatusForm
{
    [Required]
    public string Status { get; set; } = "Pending";
}

public sealed class UpdateTrackingForm
{
    public string? CourierName { get; set; }
    public string? CourierTrackingId { get; set; }
    public string? CourierTrackingUrl { get; set; }
    public string? ConsignmentId { get; set; }
}

public sealed class SaveNotesForm
{
    public string? AdminNotes { get; set; }
}

public sealed class RoleEditForm
{
    public int Id { get; set; }
    public bool Admin { get; set; }
}

public sealed class AdminUserCreateForm
{
    [Required(ErrorMessage = "Email address is required"), EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Full name is required"), StringLength(100)]
    public string FullName { get; set; } = "";

    [Required, MinLength(8, ErrorMessage = "Password must be at least 8 characters long"), StringLength(128)]
    public string Password { get; set; } = "";

    public bool IsAdmin { get; set; }

    public bool EmailConfirmed { get; set; } = true;
}

public sealed class AdminUserEditProfileForm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Email address is required"), EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Full name is required"), StringLength(100)]
    public string FullName { get; set; } = "";
}

public sealed class AdminUserPasswordResetForm
{
    public int Id { get; set; }

    [Required, MinLength(8, ErrorMessage = "Password must be at least 8 characters long"), StringLength(128)]
    public string NewPassword { get; set; } = "";
}

public sealed class StockEditForm
{
    public int VariantId { get; set; }
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
    [Required, StringLength(200)]
    public string Reason { get; set; } = "Stock count";
}

public sealed class ProductAttributesForm
{
    public int ProductId { get; set; }
    public List<ProductAttributeForm> Attributes { get; set; } = [];
}

public sealed class ProductAttributeForm
{
    public int AttributeDefinitionId { get; set; }
    public bool Enabled { get; set; }
    public bool Required { get; set; } = true;
    public List<ProductOptionForm> Options { get; set; } = [];
}

public sealed class ProductOptionForm
{
    public int AttributeOptionId { get; set; }
    public bool Enabled { get; set; }
}

public sealed class VariantGenerateForm
{
    public string Intent { get; set; } = "preview";
    public int ProductId { get; set; }
    [Required, StringLength(60)]
    public string SkuPrefix { get; set; } = "";
    [Range(0, 10000000)]
    public decimal SellingPrice { get; set; }
    [Range(0, 10000000)]
    public decimal CostPrice { get; set; }
    public List<VariantGenerationGroup> Groups { get; set; } = [];
}

public sealed class VariantGenerationGroup
{
    public int ProductAttributeDefinitionId { get; set; }
    public bool IncludeNotApplicable { get; set; }
    public List<VariantGenerationOption> Options { get; set; } = [];
}

public sealed class VariantGenerationOption
{
    public int ProductAttributeOptionId { get; set; }
    public bool Selected { get; set; }
}

public sealed record OrderableItemDto(int Id, string Label);

public enum OrderableKind
{
    ProductAttributes,
    ProductOptions,
    Carousel,
    FeaturedCategories
}

public sealed record AdminProductRow(int Id, string Name, string Slug, string Category, int VariantCount, bool Hidden, string? ImageUrl = null);

public sealed record AdminBrandRow(int Id, string Name, string Slug, string? Description, string? LogoUrl, bool IsFeatured, int SortOrder, int ProductCount, bool Hidden);

public sealed class BrandEditForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Brand name is required"), StringLength(100)]
    public string Name { get; set; } = "";
    [StringLength(120)]
    public string? Slug { get; set; } = "";
    [StringLength(2000)]
    public string? Description { get; set; }
    [StringLength(2000)]
    public string? LogoUrl { get; set; }
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public bool Hidden { get; set; }
}

public sealed record AdminVariantRow(int Id, int ProductId, string ProductName, string Sku, string Label, decimal Price, int OnHand, int Reserved, bool Active, bool Default);
