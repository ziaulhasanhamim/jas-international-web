using System.Text.RegularExpressions;

namespace JASIntWeb.Client.Services;

/// <summary>
/// Resolves next-generation image URLs (AVIF, WebP) and standard fallbacks
/// across local storage, Cloudinary CDN, and external image providers like Unsplash.
/// </summary>
public static class ImageUrlHelper
{
    private static readonly Regex CloudinaryUploadRegex = new(
        @"(https?://res\.cloudinary\.com/[^/]+/image/upload/)(?:f_[^,/]+,?)?(?:q_[^,/]+,?)?(.*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Checks whether the URL is a vector or embedded data URI which should not be converted.
    /// </summary>
    public static bool IsVectorOrDataUri(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var clean = url.Trim();
        return clean.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
               clean.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
               clean.Contains(".svg?", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates an AVIF format URL for the given image source.
    /// </summary>
    public static string? GetAvifUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || IsVectorOrDataUri(url))
            return null;

        var trimmed = url.Trim();

        // 1. Cloudinary CDN transformation
        if (trimmed.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            return TransformCloudinaryUrl(trimmed, "f_avif,q_auto");
        }

        // 2. Unsplash CDN transformation
        if (trimmed.Contains("images.unsplash.com", StringComparison.OrdinalIgnoreCase))
        {
            return TransformUnsplashUrl(trimmed, "avif");
        }

        // 3. Local relative URL or static asset (e.g. /uploads/products/.../img.jpg or /jas-logo.png)
        if (trimmed.StartsWith('/') || trimmed.StartsWith("./") || !trimmed.Contains("://"))
        {
            return ReplaceExtension(trimmed, ".avif");
        }

        // 4. Default: attempt extension replacement if it ends with common raster extensions
        return ReplaceExtension(trimmed, ".avif");
    }

    /// <summary>
    /// Generates a WebP format URL for the given image source.
    /// </summary>
    public static string? GetWebpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || IsVectorOrDataUri(url))
            return null;

        var trimmed = url.Trim();

        // 1. Cloudinary CDN transformation
        if (trimmed.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            return TransformCloudinaryUrl(trimmed, "f_webp,q_auto");
        }

        // 2. Unsplash CDN transformation
        if (trimmed.Contains("images.unsplash.com", StringComparison.OrdinalIgnoreCase))
        {
            return TransformUnsplashUrl(trimmed, "webp");
        }

        // 3. Local relative URL or static asset
        if (trimmed.StartsWith('/') || trimmed.StartsWith("./") || !trimmed.Contains("://"))
        {
            return ReplaceExtension(trimmed, ".webp");
        }

        return ReplaceExtension(trimmed, ".webp");
    }

    private static string TransformCloudinaryUrl(string url, string formatTransform)
    {
        var match = CloudinaryUploadRegex.Match(url);
        if (match.Success)
        {
            var prefix = match.Groups[1].Value;
            var suffix = match.Groups[2].Value.TrimStart('/');
            return $"{prefix}{formatTransform}/{suffix}";
        }

        var idx = url.IndexOf("/upload/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var prefix = url[..(idx + 8)];
            var rest = url[(idx + 8)..].TrimStart('/');
            return $"{prefix}{formatTransform}/{rest}";
        }

        return url;
    }

    private static string TransformUnsplashUrl(string url, string format)
    {
        var uriBuilder = new UriBuilder(url);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
        query["fm"] = format;
        if (string.IsNullOrEmpty(query["q"]))
        {
            query["q"] = "80";
        }
        uriBuilder.Query = query.ToString();
        return uriBuilder.Uri.ToString();
    }

    private static string? ReplaceExtension(string url, string newExtension)
    {
        var questionIdx = url.IndexOf('?');
        var path = questionIdx >= 0 ? url[..questionIdx] : url;
        var query = questionIdx >= 0 ? url[questionIdx..] : "";

        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
        {
            return null;
        }

        if (string.Equals(ext, newExtension, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var basePath = path[..^ext.Length];
        return $"{basePath}{newExtension}{query}";
    }
}
