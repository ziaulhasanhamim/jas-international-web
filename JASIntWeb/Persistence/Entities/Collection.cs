namespace JASIntWeb.Persistence.Entities;

public enum CollectionType
{
    Manual = 0,
    RuleBased = 1
}

public enum CollectionSortOrder
{
    ManualOrder = 0,
    Newest = 1,
    PriceLowToHigh = 2,
    PriceHighToLow = 3,
    NameAsc = 4,
    BestRated = 5,
    FeaturedFirst = 6,
    BiggestDiscount = 7
}

public sealed class Collection : SoftDeletableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? InternalNotes { get; set; }
    public CollectionType Type { get; set; } = CollectionType.Manual;
    public CollectionSortOrder SortMode { get; set; } = CollectionSortOrder.ManualOrder;
    public string? RulesJson { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CollectionProduct> Items { get; init; } = [];
    public ICollection<CollectionPage> Pages { get; init; } = [];
}

public sealed class CollectionProduct
{
    public int CollectionId { get; set; }
    public int ProductId { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    public Collection? Collection { get; set; }
    public Product? Product { get; set; }
}

public sealed class CollectionPage : AuditableEntity
{
    public int CollectionId { get; set; }
    public Collection? Collection { get; set; }

    public required string Title { get; set; }
    public required string Slug { get; set; }
    public string? Eyebrow { get; set; }
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? MobileBannerImageUrl { get; set; }
    public string ThemeColor { get; set; } = "slate";
    public bool IsPublished { get; set; } = true;
    public bool ShowFilters { get; set; } = true;

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImageUrl { get; set; }
}
