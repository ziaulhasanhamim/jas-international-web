namespace JASIntWeb.Services.Meta;

public sealed class MetaAdsOptions
{
    public const string SectionName = "Meta";

    public string? PixelId { get; set; }
    public string? AccessToken { get; set; }
    public string? TestEventCode { get; set; }
    public bool Enabled { get; set; } = true;
    public string ApiVersion { get; set; } = "v19.0";
}

public sealed record MetaAdsConfig(
    string? PixelId,
    string? AccessToken,
    string? TestEventCode,
    bool Enabled,
    bool IsConfigured
);
