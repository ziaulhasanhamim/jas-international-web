namespace JASIntWeb.Client.Dtos;

public sealed record ProductSummaryDto(int Id, string Name, string Slug, decimal Price, string Category, string Image,
    int? DefaultVariantId, int DefaultVariantStock, string? BrandName = null, string? BrandSlug = null);

public sealed record ProductDetailsDto(int Id, string Name, string Slug, string Category, string CategorySlug,
    string? Description, string Details, string MainImage, List<string> Images,
    List<ProductAttributeDto> Attributes, List<ProductVariantDto> Variants, List<CategoryLinkDto> Breadcrumbs,
    string? BrandName = null, string? BrandSlug = null);

public sealed record BrandLinkDto(int Id, string Name, string Slug, string? LogoUrl = null, bool IsFeatured = false, int SortOrder = 0);

public sealed record ProductAttributeDto(string Code, string Name, List<AttributeOptionDto> Values, bool IsRequired = true);

public sealed record AttributeOptionDto(int Id, string Code, string Value, int SortOrder);

public sealed record ProductVariantDto(int Id, string Sku, decimal Price, int Stock, bool IsDefault,
    Dictionary<string, string> Options);

public sealed record CategoryLinkDto(int Id, string Name, string Slug, int? ParentId);

public sealed record CarouselSlideDto(int Id, string? Eyebrow, string? Title, string? Copy,
    string Image, string ImageAlt, string? LinkUrl, string? LinkText)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Eyebrow + Title + Copy);
}

public sealed record DeviceOrdersQuery(List<Guid> Tokens);

public sealed record DeviceOrderDto(
    int Id,
    Guid PublicToken,
    DateTimeOffset CreatedAt,
    string Status,
    decimal Total,
    int ItemCount,
    List<string> ItemNames,
    string CustomerName);
