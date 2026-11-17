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

    public string SanitizeDescription(string? text, int maxLength = 155) => Client.Services.AdminSeoText.SanitizeDescription(text, maxLength);
    public string BuildProductTitle(string name, string? brand = null) => Client.Services.AdminSeoText.BuildProductTitle(name, brand);
    public string BuildCategoryTitle(string name) => Client.Services.AdminSeoText.BuildCategoryTitle(name);
    public string BuildBrandTitle(string name) => Client.Services.AdminSeoText.BuildBrandTitle(name);
}
