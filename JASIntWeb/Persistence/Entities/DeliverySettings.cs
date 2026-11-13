namespace JASIntWeb.Persistence.Entities;

public class DeliverySettings : Entity
{
    public string HubDistrict { get; set; } = "Khulna";
    public string? HubCity { get; set; } = "Khulna Sadar";
    public decimal HubCityFee { get; set; } = 50m;
    public decimal OutsideFee { get; set; } = 150m;
    public decimal? FreeDeliveryThreshold { get; set; }
    public bool RequireAdvanceForCOD { get; set; } = true;
    public decimal AdvanceAmount { get; set; } = 150m;
    public string BkashNumber { get; set; } = "01711465078";
    public string NagadNumber { get; set; } = "01711465078";
    public string? BanglaQrImageUrl { get; set; } = "/jas-logo.png";
    public string? PaymentInstructions { get; set; }
    public string? SmsWebhookSecret { get; set; } = "jas_sms_secret_2026";
    public string? MetaPixelId { get; set; }
    public string? MetaAccessToken { get; set; }
    public string? MetaTestEventCode { get; set; }
    public bool MetaTrackingEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
