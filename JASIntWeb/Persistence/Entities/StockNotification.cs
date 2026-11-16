namespace JASIntWeb.Persistence.Entities;

public enum StockNotificationStatus { Pending, Active, Sent, Unsubscribed, Failed }

public sealed class StockNotification : AuditableEntity
{
    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public Guid Token { get; set; } = Guid.NewGuid();
    public StockNotificationStatus Status { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? ConfirmationSentAt { get; set; }
    public DateTimeOffset? NotificationSentAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
