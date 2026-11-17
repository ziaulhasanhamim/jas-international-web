using System.Text.RegularExpressions;
namespace JASIntWeb.Client.Services;

public static class AdminSeoText
{
    public static string CategoryDescription(string name) => $"Browse 100% authentic {name} with official warranty, COD and nationwide delivery from JAS International.";
    public static string BrandDescription(string name, string? description) => string.IsNullOrWhiteSpace(description) ? $"Browse 100% authentic {name} gadgets and accessories at JAS International. Official warranty and fast delivery across Bangladesh." : description;
    public static string ProductDescription(string name, string? description) => string.IsNullOrWhiteSpace(description) ? $"{name} available at JAS International. Authentic tech gadgets with official warranty and nationwide delivery in Bangladesh." : description;

    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.None, TimeSpan.FromSeconds(1));
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(1));
    public static string SanitizeDescription(string? htmlOrText, int maxLength = 155)
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

    public static string BuildProductTitle(string productName, string? brandName = null)
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

    public static string BuildCategoryTitle(string categoryName)
    {
        var candidate = $"{categoryName} Price in Bangladesh | JAS International";
        return candidate.Length <= 60 ? candidate : $"{categoryName} in Bangladesh | JAS International";
    }

    public static string BuildBrandTitle(string brandName)
    {
        var candidate = $"{brandName} Products & Price in Bangladesh | JAS International";
        return candidate.Length <= 60 ? candidate : $"{brandName} in Bangladesh | JAS International";
    }
}
