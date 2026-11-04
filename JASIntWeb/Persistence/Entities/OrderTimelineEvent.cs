namespace JASIntWeb.Persistence.Entities;

public sealed class OrderTimelineEvent : AuditableEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Actor { get; set; }
    public string? BadgeColor { get; set; }
}
