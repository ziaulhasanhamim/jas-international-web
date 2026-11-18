using System.Text.RegularExpressions;

namespace JASIntWeb.Client.Services;

public static partial class ImageUrlHelper
{
    [GeneratedRegex(@"/images/(?<kind>static|animated)/[a-f0-9]{32,64}\.(?:webp|avif)$", RegexOptions.CultureInvariant)]
    private static partial Regex ManagedImage();

    public static bool IsVectorOrDataUri(string? url) => string.IsNullOrWhiteSpace(url)
        || url.Trim().StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        || url.Split('?', '#')[0].EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    // Only managed static images guarantee a companion. Never invent URLs for external providers.
    public static string? GetAvifUrl(string? url) => IsManaged(url, out var kind) && kind == "static" ? ChangeFormat(url!, ".avif") : null;
    public static string? GetWebpUrl(string? url) => IsManaged(url, out _) ? ChangeFormat(url!, ".webp") : null;
    public static string GetAutoOptimizedUrl(string? url) => GetWebpUrl(url) ?? url?.Trim() ?? "";

    private static bool IsManaged(string? value, out string kind)
    {
        kind = "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        var match = ManagedImage().Match(uri.AbsolutePath);
        if (!match.Success) return false;
        kind = match.Groups["kind"].Value;
        return true;
    }
    private static string ChangeFormat(string url, string extension) => url[..url.LastIndexOf('.')] + extension;
}
