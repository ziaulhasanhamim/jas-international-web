using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JASIntWeb.Services.MediaMigration;

public sealed class MediaMigrationManifest
{
    public int Version { get; set; } = 1;
    public required string DatabaseFingerprint { get; set; }
    public required string DestinationBaseUrl { get; set; }
    public List<MediaMigrationAsset> Assets { get; set; } = [];
    public List<MediaMigrationReference> References { get; set; } = [];
    public bool Applied { get; set; }
    public bool RolledBack { get; set; }
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static async Task SaveAsync(string path, MediaMigrationManifest manifest, CancellationToken ct)
    {
        var temp = path + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using (var stream = new FileStream(temp, options))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions { WriteIndented = true }, ct);
            await stream.FlushAsync(ct);
        }
        File.Move(temp, path, true);
    }
}
public sealed class MediaMigrationAsset
{
    public required string SourceUrl { get; set; }
    public required string Identity { get; set; }
    public string? DestinationUrl { get; set; }
    public string? SourceSha256 { get; set; }
    public Dictionary<string, string> VerifiedObjects { get; set; } = [];
}
public sealed record MediaMigrationReference(string Table, string Column, int Id, int? ProductId, string Before, string After);

public static partial class LegacyMediaUrls
{
    [GeneratedRegex("https?://res\\.cloudinary\\.com/[^\\s\"'<>\\)\\]]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Urls();
    public static IEnumerable<string> Find(string value) => Urls().Matches(value).Select(match => match.Value).Distinct(StringComparer.Ordinal);
    public static string Canonical(string source)
    {
        var decoded = System.Net.WebUtility.HtmlDecode(source);
        var uri = new Uri(decoded);
        if (uri.Scheme != "https" || uri.Host != "res.cloudinary.com" || !uri.AbsolutePath.Contains("/image/upload/", StringComparison.Ordinal))
            throw new InvalidDataException("Only HTTPS legacy image uploads can be migrated.");
        // Remove format/quality negotiation, retaining crop/resize/overlay semantics and source version.
        var segments = uri.AbsolutePath.Split('/');
        var start = Array.IndexOf(segments, "upload") + 1;
        List<string> transformed = [.. segments.Take(start)];
        for (var index = start; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (segment.StartsWith('v') && segment.Length > 1 && segment[1..].All(char.IsAsciiDigit))
            { transformed.AddRange(segments.Skip(index)); break; }
            if (segment.Contains(',') || Regex.IsMatch(segment, @"^(f|q|c|w|h|g|ar|e|a|dpr|fl|b|bo|r|l|u|x|y)_"))
            {
                var kept = segment.Split(',').Where(part => !part.StartsWith("f_", StringComparison.Ordinal) && !part.StartsWith("q_", StringComparison.Ordinal));
                var result = string.Join(',', kept);
                if (result.Length != 0) transformed.Add(result);
            }
            else { transformed.AddRange(segments.Skip(index)); break; }
        }
        return "https://res.cloudinary.com" + string.Join('/', transformed);
    }
    public static string Replace(string value, IReadOnlyDictionary<string, string> replacements) =>
        Urls().Replace(value, match => replacements.TryGetValue(Canonical(match.Value), out var replacement) ? replacement : throw new InvalidDataException("A referenced image has not been verified."));
}
