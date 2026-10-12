namespace JASIntWeb.Services;

public static class AccountNavigation
{
    public static string SafeReturn(string? url) => !string.IsNullOrWhiteSpace(url) && url.StartsWith('/')
    &&
    !url.StartsWith("//")
    && !url.Contains('\\')
    && !url.Any(char.IsControl) ? url : "/cart";
}
