using System.Text.RegularExpressions;

namespace JASIntWeb.Services.Analytics;

public static class UserAgentParser
{
    private static readonly Regex BotRegex = new(
        @"(bot|crawler|spider|crawling|slurp|facebookexternalhit|meta-externalagent|whatsapp|telegrambot|twitterbot|pinterest|googlebot|bingbot|yandex|duckduckbot|baiduspider|semrush|ahrefs|uptime|pingdom)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsBot(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return false;
        return BotRegex.IsMatch(userAgent);
    }

    public static (string DeviceType, string Browser, string OperatingSystem) Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return ("Desktop", "Other", "Other");
        }

        var ua = userAgent.Trim();

        // 1. Device Type
        var deviceType = "Desktop";
        if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
            (ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase)) ||
            (ua.Contains("Android", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase)))
        {
            deviceType = "Tablet";
        }
        else if (ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                 ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                 ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                 ua.Contains("BlackBerry", StringComparison.OrdinalIgnoreCase))
        {
            deviceType = "Mobile";
        }

        // 2. Operating System
        var os = "Other";
        if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) os = "Android";
        else if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) || ua.Contains("iOS", StringComparison.OrdinalIgnoreCase)) os = "iOS";
        else if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase)) os = "Windows";
        else if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)) os = "macOS";
        else if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)) os = "Linux";
        else if (ua.Contains("CrOS", StringComparison.OrdinalIgnoreCase)) os = "ChromeOS";

        // 3. Browser
        var browser = "Other";
        if (ua.Contains("SamsungBrowser", StringComparison.OrdinalIgnoreCase)) browser = "Samsung Internet";
        else if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) || ua.Contains("Edge/", StringComparison.OrdinalIgnoreCase)) browser = "Edge";
        else if (ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase) || ua.Contains("Opera", StringComparison.OrdinalIgnoreCase)) browser = "Opera";
        else if (ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chromium", StringComparison.OrdinalIgnoreCase)) browser = "Chrome";
        else if (ua.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) browser = "Safari";
        else if (ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) browser = "Firefox";

        return (deviceType, browser, os);
    }

    public static string AnonymizeIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return "0.0.0.0";
        var clean = ip.Trim();

        // IPv4: 103.145.24.12 -> 103.145.24.0 (anonymized standard)
        var lastDot = clean.LastIndexOf('.');
        if (lastDot > 0 && clean.Count(c => c == '.') == 3)
        {
            return clean[..(lastDot + 1)] + "0";
        }

        // IPv6: truncate to /64
        var colonIdx = clean.IndexOf(':');
        if (colonIdx > 0)
        {
            var parts = clean.Split(':');
            if (parts.Length > 4)
            {
                return string.Join(":", parts.Take(4)) + "::";
            }
        }

        return clean;
    }

    public static string ExtractDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "Direct";
        var trimmed = url.Trim();
        if (trimmed.Equals("direct", StringComparison.OrdinalIgnoreCase)) return "Direct";

        try
        {
            if (!trimmed.Contains("://")) trimmed = "https://" + trimmed;
            var uri = new Uri(trimmed);
            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host[4..];
            if (host.Contains("google.")) return "google.com";
            if (host.Contains("facebook.")) return "facebook.com";
            if (host.Contains("instagram.")) return "instagram.com";
            if (host.Contains("youtube.")) return "youtube.com";
            if (host.Contains("whatsapp.")) return "whatsapp.com";
            if (host.Contains("tiktok.")) return "tiktok.com";
            if (host.Contains("jasinternational.com.bd") || host.Contains("localhost")) return "Internal";
            return host;
        }
        catch
        {
            return "Other";
        }
    }
}
