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
        static string Part(string text, int length)
        {
            var code = Regex.Replace(text.ToUpperInvariant(), "[^A-Z0-9]+", "-").Trim('-');
            return code.Length > length ? code[..length].TrimEnd('-') : code;
        }
        var title = Part(draft.Information.Name, 40);
        var values = draft.Attributes.Where(a => !a.Retired).Select(a => a.Options.FirstOrDefault(o => variant.Choices.GetValueOrDefault(a.DefinitionId) == o.ClientId)?.Value).Where(v => v is not null);
        var options = Part(string.Join('-', values), 40);
        return $"JAS-{(title.Length == 0 ? "PRODUCT" : title)}-{(options.Length == 0 ? "BASE" : options)}-{variant.ClientId.ToString("N")[..8].ToUpperInvariant()}";
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
