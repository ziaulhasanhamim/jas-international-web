namespace JASIntWeb.Client.Services;

public static class CategoryVisuals
{
    public static bool IsImage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || value.Contains('\\')) return false;
        return value.StartsWith('/') && !value.StartsWith("//")
            || Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";
    }
    public static bool IsText(string? value) => !string.IsNullOrWhiteSpace(value) && !value.Contains(":") && !value.Contains('/') && value.Length <= 30;
}
