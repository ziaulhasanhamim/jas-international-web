namespace JASIntWeb.Persistence.Entities;

public class VisitorLog
{
    public long Id { get; set; }

    public string VisitorId { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    public int? UserId { get; set; }

    public string Path { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Referrer { get; set; }

    public string? ReferrerDomain { get; set; }

    public string? IpAddress { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string DeviceType { get; set; } = "Desktop"; // Mobile, Desktop, Tablet

    public string Browser { get; set; } = "Other"; // Chrome, Safari, etc.

    public string OperatingSystem { get; set; } = "Other"; // Android, iOS, Windows, macOS, Linux

    public string? UserAgent { get; set; }

    public string? UtmSource { get; set; }

    public string? UtmMedium { get; set; }

    public string? UtmCampaign { get; set; }

    public string? Fbclid { get; set; }

    public int DurationSeconds { get; set; } = 0;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public bool IsBounce { get; set; } = true;

    public int? ConvertedOrderId { get; set; }
}
