namespace JASIntWeb.Services.Meta;

public sealed class MetaAdsOptions
{
    public const string SectionName = "Meta";

    public string? PixelId { get; set; }
    public string? AccessToken { get; set; }
    public string? TestEventCode { get; set; }
    public bool Enabled { get; set; } = true;
    public const string DefaultApiVersion = "v25.0";
    public string ApiVersion { get; set; } = DefaultApiVersion;

    public static string EventsUrl(string? apiVersion, string pixelId, string accessToken)
    {
        var version = string.IsNullOrWhiteSpace(apiVersion) ? DefaultApiVersion : apiVersion.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^v[0-9]+\.[0-9]+$"))
            throw new InvalidOperationException("Meta:ApiVersion must use the format v25.0.");
        return $"https://graph.facebook.com/{version}/{Uri.EscapeDataString(pixelId)}/events?access_token={Uri.EscapeDataString(accessToken)}";
    }
}

public sealed record MetaAdsConfig(
    string? PixelId,
    string? AccessToken,
    string? TestEventCode,
    bool Enabled,
    bool IsConfigured
);
