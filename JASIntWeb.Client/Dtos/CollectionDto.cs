using System.Text.Json.Serialization;

namespace JASIntWeb.Client.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectionRuleLogicalOp
{
    And = 0,
    Or = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectionRuleField
{
    Tag = 0,
    Category = 1,
    Brand = 2,
    Price = 3,
    Stock = 4,
    HasDiscount = 5,
    DiscountPercent = 6,
    IsFeatured = 7,
    IsNewArrival = 8,
    Title = 9
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectionRuleOperator
{
    Equals = 0,
    NotEquals = 1,
    GreaterThan = 2,
    GreaterThanOrEqual = 3,
    LessThan = 4,
    LessThanOrEqual = 5,
    Contains = 6,
    NotContains = 7,
    IsTrue = 8,
    IsFalse = 9
}

public sealed class CollectionRuleCondition
{
    public CollectionRuleField Field { get; set; } = CollectionRuleField.Category;
    public CollectionRuleOperator Operator { get; set; } = CollectionRuleOperator.Equals;
    public string? Value { get; set; }
}

public sealed class CollectionRuleGroup
{
    public CollectionRuleLogicalOp MatchOperator { get; set; } = CollectionRuleLogicalOp.And;
    public List<CollectionRuleCondition> Rules { get; set; } = [];
    public List<CollectionRuleGroup> Groups { get; set; } = [];
}

public sealed record CollectionPageDetailsDto(
    int Id,
    int CollectionId,
    string CollectionName,
    string Title,
    string Slug,
    string? Eyebrow,
    string? Description,
    string? BannerImageUrl,
    string? MobileBannerImageUrl,
    string ThemeColor,
    bool ShowFilters,
    string? MetaTitle,
    string? MetaDescription,
    string? OgImageUrl,
    List<ProductSummaryDto> Products,
    int TotalCount,
    List<BrandLinkDto> AvailableBrands
);
