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
    public string? SmsWebhookSecret { get; set; }
    public string? WhatsAppNumber { get; set; } = "8801711465078";
    public string? MessengerPageId { get; set; } = "international.jas";
    public string? FacebookDomainVerification { get; set; }
    public string? GoogleSearchConsoleVerification { get; set; }
    public string MetaPurchaseValueMode { get; set; } = "NetSubtotal";
    public string? CatalogFeedToken { get; set; }
    public string? MetaPixelId { get; set; }
    public string? MetaAccessToken { get; set; }
    public string? MetaTestEventCode { get; set; }
    public bool MetaTrackingEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
