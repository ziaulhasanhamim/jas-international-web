using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JASIntWeb.Client.Contracts;
namespace JASIntWeb.Client.Services;

public static partial class ProductEditorRules
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")] private static partial Regex Hex();
    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$")] private static partial Regex Key();
    public static Guid Identity(string kind, int id) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}:{id}"))[..16]);
    public static string Signature(IEnumerable<int> options) => string.Join('-', options.Order());
    public static string ChoiceKey(IReadOnlyDictionary<int, Guid> choices) => string.Join('|', choices.Where(c => c.Value != Guid.Empty).OrderBy(c => c.Key).Select(c => $"{c.Key}:{c.Value:N}"));
    public static string AutoSku(ProductEditorDraft draft, EditorVariant variant)
    {
        var prodCode = ShortProductCode(draft.Information?.Name, draft.Id);
        var optCode = ShortOptionCode(draft, variant);
        var clientSuffix = variant.ClientId.ToString("N")[..4].ToUpperInvariant();
        return $"JAS-{prodCode}-{optCode}-{clientSuffix}";
    }

    private static string ShortProductCode(string? name, int id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return id > 0 ? id.ToString() : "PROD";
        }

        var words = Regex.Matches(name, @"[A-Za-z0-9]+")
            .Select(m => m.Value.ToUpperInvariant())
            .ToList();

        if (words.Count == 0)
        {
            return id > 0 ? id.ToString() : "PROD";
        }

        if (words.Count == 1)
        {
            var w = words[0];
            return w.Length <= 6 ? w : w[..6];
        }

        if (words.Count == 2)
        {
            var w1 = words[0].Length <= 3 ? words[0] : words[0][..3];
            var w2 = words[1].Length <= 4 ? words[1] : words[1][..4];
            return $"{w1}-{w2}";
        }

        // 3+ words: first word (up to 3 chars) + initials/numbers for subsequent words
        var first = words[0].Length <= 3 ? words[0] : words[0][..3];
        var rest = new StringBuilder();

        for (var i = 1; i < words.Count && rest.Length < 6; i++)
        {
            var word = words[i];
            if (word.Any(char.IsAsciiDigit))
            {
                rest.Append(word.Length <= 4 ? word : word[..4]);
            }
            else
            {
                rest.Append(word[0]);
            }
        }

        var suffix = rest.ToString();
        return suffix.Length > 0 ? $"{first}-{suffix}" : first;
    }

    private static string ShortOptionCode(ProductEditorDraft draft, EditorVariant variant)
    {
        var options = draft.Attributes
            .Where(a => !a.Retired)
            .Select(a => a.Options.FirstOrDefault(o => variant.Choices.GetValueOrDefault(a.DefinitionId) == o.ClientId))
            .Where(o => o is not null)
            .ToList();

        if (options.Count == 0) return "BASE";

        var codes = new List<string>();
        foreach (var opt in options)
        {
            var code = !string.IsNullOrWhiteSpace(opt!.Code)
                ? opt.Code.Trim().ToUpperInvariant()
                : AbbreviateOption(opt.Value);

            if (!string.IsNullOrEmpty(code))
            {
                codes.Add(code);
            }
        }

        return codes.Count > 0 ? string.Join('-', codes) : "BASE";
    }

    private static string AbbreviateOption(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var clean = value.Trim().ToUpperInvariant();

        return clean switch
        {
            "BLACK" => "BLK",
            "WHITE" => "WHT",
            "SILVER" => "SLV",
            "BLUE" => "BLU",
            "GREEN" => "GRN",
            "RED" => "RED",
            "GOLD" => "GLD",
            "GRAY" or "GREY" => "GRY",
            "ORANGE" => "ORG",
            "YELLOW" => "YLW",
            "PURPLE" => "PRP",
            "PINK" => "PNK",
            "MIDNIGHT BLACK" => "MBLK",
            "SPACE GRAY" or "SPACE GREY" => "SGRY",
            "ASH GRAY" or "ASH GREY" => "AGRY",
            "DARK BLUE" => "DBLU",
            "NAVY BLUE" => "NVY",
            _ => ExtractGeneralOption(clean)
        };
    }

    private static string ExtractGeneralOption(string clean)
    {
        var words = Regex.Matches(clean, @"[A-Za-z0-9]+")
            .Select(m => m.Value)
            .ToList();

        if (words.Count == 0) return "";

        if (words.Any(w => w.Any(char.IsAsciiDigit)))
        {
            var combined = string.Concat(words);
            return combined.Length <= 5 ? combined : combined[..5];
        }

        if (words.Count == 1)
        {
            return words[0].Length <= 4 ? words[0] : words[0][..3];
        }

        var initials = string.Concat(words.Select(w => w[0]));
        return initials.Length <= 4 ? initials : initials[..4];
    }
    public static void ValidateFields(IReadOnlyList<ValueField> fields, string? swatch)
    {
        if (fields.Count > 16 || fields.Select(f => f?.Key).Distinct(StringComparer.Ordinal).Count() != fields.Count
            || fields.Any(f => f is null || f.Key is null || !Key().IsMatch(f.Key) || string.IsNullOrWhiteSpace(f.Label) || f.Label.Length > 80 || !Enum.IsDefined(f.Kind)))
            throw new ArgumentException("Use up to 16 distinct fields with stable lowercase keys and labels of 1–80 characters.");
        if (!string.IsNullOrWhiteSpace(swatch) && !fields.Any(f => f.Key == swatch && f.Kind == ValueFieldKind.HexColor))
            throw new ArgumentException("Select a hex-color field for swatches.");
    }
    public static string EncodeMetadata(IReadOnlyList<ValueField> fields, IReadOnlyDictionary<string, string> values)
    {
        Dictionary<string, object> result = [];
        foreach (var (key, value) in values)
        {
            if (string.IsNullOrEmpty(value)) continue;
            var field = fields.SingleOrDefault(f => f.Key == key) ?? throw new ArgumentException($"Unknown metadata field {key}.");
            result[key] = field.Kind switch
            {
                ValueFieldKind.Text when value.Length <= 500 => value,
                ValueFieldKind.Number when decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
                ValueFieldKind.Boolean when bool.TryParse(value, out var flag) => flag,
                ValueFieldKind.HexColor when Hex().IsMatch(value) => value.ToUpperInvariant(),
                _ => throw new ArgumentException($"Enter a valid {field.Kind} value for {field.Label}.")
            };
        }
        return JsonSerializer.Serialize(result);
    }
    public static Dictionary<string, string> DecodeMetadata(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText());
    }
    public static List<ValueField> DecodeFields(string json) => JsonSerializer.Deserialize<List<ValueField>>(json) ?? [];
    public static string? Swatch(string json, string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return null;
        var value = DecodeMetadata(json).GetValueOrDefault(field);
        return value is not null && Hex().IsMatch(value) ? value.ToUpperInvariant() : null;
    }
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
