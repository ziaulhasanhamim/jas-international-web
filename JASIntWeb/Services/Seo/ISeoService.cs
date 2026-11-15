using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace JASIntWeb.Services.Seo;

public interface ISeoService
{
    string PublicBaseUrl { get; }
    string BuildAbsoluteUrl(string? path);
    string BuildCanonicalUrl(string pathAndQuery);
    string SanitizeDescription(string? htmlOrText, int maxLength = 155);
    string BuildProductTitle(string productName, string? brandName = null);
    string BuildCategoryTitle(string categoryName);
    string BuildBrandTitle(string brandName);
}

public sealed class SeoService(IConfiguration configuration) : ISeoService
{
    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public string PublicBaseUrl
    {
        get
        {
            var raw = configuration["PublicBaseUrl"] ?? "https://www.jasinternational.com.bd";
            return raw.TrimEnd('/');
        }
    }

    public string BuildAbsoluteUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PublicBaseUrl + "/";
        }

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var normalized = path.StartsWith('/') ? path : "/" + path;
        return PublicBaseUrl + normalized;
    }

    public string BuildCanonicalUrl(string pathAndQuery)
    {
        if (string.IsNullOrWhiteSpace(pathAndQuery))
        {
            return PublicBaseUrl;
        }

        var fragmentIndex = pathAndQuery.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            pathAndQuery = pathAndQuery[..fragmentIndex];
        }

        var questionIndex = pathAndQuery.IndexOf('?');
        var path = questionIndex >= 0 ? pathAndQuery[..questionIndex] : pathAndQuery;
        var queryString = questionIndex >= 0 ? pathAndQuery[questionIndex..] : string.Empty;

        // Clean & lowercase path
        path = path.Trim().ToLowerInvariant();
        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }
        if (path == "/")
        {
            path = string.Empty;
        }

        // Whitelist only legitimate pagination: keep ?page=X if X > 1
        var allowedQuery = string.Empty;
        if (!string.IsNullOrEmpty(queryString))
        {
            var parsed = QueryHelpers.ParseQuery(queryString);
            if (parsed.TryGetValue("page", out var pageValues) &&
                int.TryParse(pageValues.FirstOrDefault(), out var pageNum) &&
                pageNum > 1)
            {
                allowedQuery = $"?page={pageNum}";
            }
        }

        return PublicBaseUrl + path + allowedQuery;
    }

    public string SanitizeDescription(string? htmlOrText, int maxLength = 155)
    {
        if (string.IsNullOrWhiteSpace(htmlOrText))
        {
            return "Authentic tech gadgets, smartwatches, wireless audio and accessories with official warranty and fast nationwide delivery from JAS International.";
        }

        var text = HtmlTagRegex.Replace(htmlOrText, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = WhitespaceRegex.Replace(text, " ").Trim();

        if (text.Length <= maxLength)
        {
            return text;
        }

        var truncated = text[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength - 30)
        {
            truncated = truncated[..lastSpace];
        }

        return truncated.TrimEnd('.', ',', ';', ' ') + "…";
    }

    public string BuildProductTitle(string productName, string? brandName = null)
    {
        var cleaned = productName.Trim();
        if (!string.IsNullOrWhiteSpace(brandName) && !cleaned.StartsWith(brandName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            cleaned = $"{brandName.Trim()} {cleaned}";
        }

        var candidate = $"{cleaned} Price in Bangladesh | JAS International";
        if (candidate.Length <= 60) return candidate;

        candidate = $"{cleaned} Price in BD | JAS International";
        if (candidate.Length <= 60) return candidate;

        if (cleaned.Length > 40)
        {
            cleaned = cleaned[..38].TrimEnd() + "…";
        }
        return $"{cleaned} | JAS International";
    }

    public string BuildCategoryTitle(string categoryName)
    {
        var candidate = $"{categoryName} Price in Bangladesh | JAS International";
        return candidate.Length <= 60 ? candidate : $"{categoryName} in Bangladesh | JAS International";
    }

    public string BuildBrandTitle(string brandName)
    {
        var candidate = $"{brandName} Products & Price in Bangladesh | JAS International";
        return candidate.Length <= 60 ? candidate : $"{brandName} in Bangladesh | JAS International";
    }
}
