namespace JASIntWeb.Persistence.Entities;

public sealed class CarouselSlide : Entity
{
    public string? Eyebrow { get; set; }
    public string? Title { get; set; }
    public string? Copy { get; set; }
    public required string ImageUrl { get; set; }
    public required string ImageAlt { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CartMergeReceipt : Entity
{
    public int UserId { get; set; }
    public Guid Token { get; set; }
}
