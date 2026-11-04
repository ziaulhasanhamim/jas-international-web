namespace JASIntWeb.Persistence.Entities;

public sealed class CuratedPromoCard : Entity
{
    public string Eyebrow { get; set; } = "FLAGSHIP";
    public string Title { get; set; } = "Featured";
    public string Subtitle { get; set; } = "";
    public string LinkUrl { get; set; } = "/products";
    public string LinkText { get; set; } = "Explore";
    public string Theme { get; set; } = "navy"; // navy, amber, indigo, emerald, crimson
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
