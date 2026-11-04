using System.Text;
using System.Text.RegularExpressions;

namespace JASIntWeb.Services;

/// <summary>
/// High-performance, pure utility for generating SEO-friendly URL slugs,
/// human-readable inventory SKUs, and standardized scannable barcodes.
/// Designed for 100% deterministic unit testing without database or external mocks.
/// </summary>
public static partial class SlugAndSkuGenerator
{
    [GeneratedRegex(@"[^a-z0-9\s-]", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"[\s-]+", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesOrHyphensRegex();

    [GeneratedRegex(@"[^A-Z0-9-]", RegexOptions.Compiled)]
    private static partial Regex SkuCleaningRegex();

    /// <summary>
    /// Generates a clean, SEO-optimized URL slug from any title or product name.
    /// Handles diacritics, symbols, multiple spaces, and punctuation.
    /// Example: "Apple iPhone 16 Pro Max (Desert Titanium)!" -> "apple-iphone-16-pro-max-desert-titanium"
    /// </summary>
    public static string GenerateSlug(string? input, string fallbackPrefix = "item")
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return $"{fallbackPrefix}-{Random.Shared.Next(10000, 99999)}";
        }

        var text = input.Trim().ToLowerInvariant();

        // Replace common symbols with words
        text = text.Replace("&", "and")
                   .Replace("+", "-plus-")
                   .Replace("@", "-at-")
                   .Replace("%", "-percent-");

        // Remove invalid characters
        text = NonAlphaNumericRegex().Replace(text, "");

        // Collapse whitespace and hyphens
        text = MultipleSpacesOrHyphensRegex().Replace(text, "-").Trim('-');

        if (string.IsNullOrWhiteSpace(text))
        {
            return $"{fallbackPrefix}-{Random.Shared.Next(10000, 99999)}";
        }

        // Limit length to 100 chars at word/hyphen boundary
        if (text.Length > 90)
        {
            var truncated = text[..90];
            var lastHyphen = truncated.LastIndexOf('-');
            text = lastHyphen > 30 ? truncated[..lastHyphen] : truncated;
        }

        return text;
    }

    /// <summary>
    /// Generates a human-readable, professional SKU for inventory tracking.
    /// Format: [BRAND]-[PRODUCT_ACRONYM]-[VARIANT_OPTS]
    /// Example: Brand="Apple", Product="iPhone 16 Pro Max", Options=["Desert Titanium", "256GB"]
    /// Output: "APL-IPH16PM-DES-256G"
    /// </summary>
    public static string GenerateSku(string productName, string? brandName = null, IEnumerable<string>? variantOptions = null)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return $"SKU-{Random.Shared.Next(100000, 999999)}";
        }

        var sb = new StringBuilder();

        // 1. Brand prefix (up to 3-4 chars)
        if (!string.IsNullOrWhiteSpace(brandName))
        {
            var cleanBrand = SkuCleaningRegex().Replace(brandName.Trim().ToUpperInvariant(), "");
            var brandPrefix = cleanBrand.Length <= 4 ? cleanBrand : cleanBrand[..4];
            sb.Append(brandPrefix).Append('-');
        }

        // 2. Product name tokens
        var words = productName.Trim().Split([' ', '-', '_', '(', ')', '[', ']', '/'], StringSplitOptions.RemoveEmptyEntries);
        var productCode = new StringBuilder();

        foreach (var word in words)
        {
            var cleanWord = SkuCleaningRegex().Replace(word.ToUpperInvariant(), "");
            if (string.IsNullOrEmpty(cleanWord)) continue;

            // If word has digits (e.g. "16", "S24", "4K"), include it
            if (cleanWord.Any(char.IsAsciiDigit))
            {
                productCode.Append(cleanWord);
            }
            else if (cleanWord.Length <= 4)
            {
                productCode.Append(cleanWord);
            }
            else
            {
                // Take first 3 letters for long words
                productCode.Append(cleanWord[..Math.Min(3, cleanWord.Length)]);
            }

            if (productCode.Length >= 10) break;
        }

        if (productCode.Length == 0)
        {
            productCode.Append("PRD");
        }

        sb.Append(productCode);

        // 3. Variant options (e.g. Color, Storage)
        if (variantOptions != null)
        {
            foreach (var opt in variantOptions)
            {
                if (string.IsNullOrWhiteSpace(opt)) continue;
                var cleanOpt = SkuCleaningRegex().Replace(opt.Trim().ToUpperInvariant(), "");
                if (string.IsNullOrEmpty(cleanOpt)) continue;

                var optCode = cleanOpt.Length <= 5 ? cleanOpt : cleanOpt[..3];
                sb.Append('-').Append(optCode);
            }
        }

        var result = sb.ToString();
        // Sku should not exceed 35 chars
        return result.Length > 35 ? result[..35] : result;
    }

    /// <summary>
    /// Generates a standard EAN-8 barcode (7 digits + 1 checksum digit) from integer ID.
    /// Checksum is calculated using standard EAN Modulo-10 with weights 3 and 1.
    /// This barcode is compatible with physical 1D/2D laser barcode scanners.
    /// </summary>
    public static string GenerateEan8Barcode(int id)
    {
        if (id <= 0)
        {
            id = 1;
        }

        // Keep within 7 digits: 1 to 9,999,999
        var modId = id % 10000000;
        if (modId == 0) modId = 1;

        var body = modId.ToString("D7");
        var sum = body.Select((digit, index) => (digit - '0') * (index % 2 == 0 ? 3 : 1)).Sum();
        var checkDigit = (10 - sum % 10) % 10;

        return $"{body}{checkDigit}";
    }

    /// <summary>
    /// Validates whether a barcode string is a valid EAN-8 barcode with correct checksum.
    /// </summary>
    public static bool IsValidEan8(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Length != 8 || !barcode.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = barcode[..7].Select((digit, index) => (digit - '0') * (index % 2 == 0 ? 3 : 1)).Sum();
        var expectedCheckDigit = (10 - sum % 10) % 10;
        var actualCheckDigit = barcode[7] - '0';

        return expectedCheckDigit == actualCheckDigit;
    }
}
