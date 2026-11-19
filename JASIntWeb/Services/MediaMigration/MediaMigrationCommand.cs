using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageMagick;
using JASIntWeb.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Services.MediaMigration;

public static class MediaMigrationCommand
{
    public static async Task<bool> RunMediaMigrationAsync(this WebApplication app)
    {
        var config = app.Configuration;
        if (config["MediaMigration:Action"] is not { } action) return false;

        if (action == "sync-local")
        {
            return await SyncLocalMediaAsync(app);
        }

        var path = config["MediaMigration:Manifest"] ?? throw new InvalidOperationException("A private migration manifest path is required.");
        await using var scope = app.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var references = new MediaReferenceMigration(db);
        var options = app.Services.GetRequiredService<S3StorageOptions>();
        if (!options.IsConfigured) throw new InvalidOperationException("Configure the destination storage before migrating.");
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) };
        MediaMigrationManifest manifest;
        if (action == "prepare")
        {
            if (File.Exists(path)) throw new InvalidOperationException("Manifest already exists. Resume it instead of overwriting the backup.");
            manifest = new() { DatabaseFingerprint = references.Fingerprint(), DestinationBaseUrl = options.PublicBaseUrl.TrimEnd('/'), References = await references.ScanAsync() };
            var assets = await InventoryAsync(config, http);
            foreach (var url in manifest.References.SelectMany(reference => LegacyMediaUrls.Find(reference.Before))) assets.Add(LegacyMediaUrls.Canonical(url));
            manifest.Assets = assets.Order().Select(url => new MediaMigrationAsset { SourceUrl = url, Identity = MediaMigrationManifest.Hash(url) }).ToList();
            await MediaMigrationManifest.SaveAsync(path, manifest, default);
        }
        else
        {
            manifest = JsonSerializer.Deserialize<MediaMigrationManifest>(await File.ReadAllTextAsync(path)) ?? throw new InvalidDataException("Invalid manifest.");
            if (manifest.Version != 1 || manifest.DatabaseFingerprint != references.Fingerprint() || manifest.DestinationBaseUrl != options.PublicBaseUrl.TrimEnd('/'))
                throw new InvalidOperationException("Manifest target mismatch.");
            if (action is "copy" or "verify")
            {
                var storage = (S3StorageService)app.Services.GetRequiredService<IStorageService>();
                foreach (var asset in manifest.Assets)
                {
                    try
                    {
                        if (asset.DestinationUrl is not null && asset.VerifiedObjects.Count != 0)
                        {
                            try { await VerifyAsync(http, asset, options); continue; }
                            catch when (action == "copy") { /* Incomplete remote objects are repaired using the same keys. */ }
                        }
                        if (action == "verify") throw new InvalidOperationException("An asset has not been copied.");
                        var sourceUri = new Uri(asset.SourceUrl);
                        if (sourceUri.Host != "res.cloudinary.com" || sourceUri.Scheme != "https") throw new InvalidDataException("Invalid source host.");
                        using var response = await http.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        var bytes = await ReadBoundedAsync(response);
                        var sourceHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                        if (asset.SourceSha256 is not null && asset.SourceSha256 != sourceHash) throw new InvalidOperationException("Source changed since the last attempt.");
                        asset.SourceSha256 = sourceHash;
                        using var stream = new MemoryStream(bytes);
                        var adapter = new MigrationUpload(storage, asset.Identity);
                        asset.DestinationUrl = await ImageUploadValidator.UploadAsync(adapter, stream, Path.GetFileName(sourceUri.AbsolutePath), response.Content.Headers.ContentType?.MediaType ?? "", default);
                        asset.VerifiedObjects.Clear();
                        await VerifyAsync(http, asset, options);
                        await MediaMigrationManifest.SaveAsync(path, manifest, default);
                        Console.WriteLine($"Verified media {manifest.Assets.IndexOf(asset) + 1}/{manifest.Assets.Count}");
                    }
                    catch
                    {
                        // Report the identity, never source credentials, HTTP error bodies or database details.
                        Console.Error.WriteLine($"Media operation failed for identity {asset.Identity}. Manifest retained for retry.");
                        throw;
                    }
                }
                var replacements = manifest.Assets.ToDictionary(asset => asset.SourceUrl, asset => asset.DestinationUrl!);
                manifest.References = manifest.References.Select(reference => reference with { After = LegacyMediaUrls.Replace(reference.Before, replacements) }).ToList();
            }
            else if (action is "apply" or "rollback")
            {
                if (config["MediaMigration:ConfirmTarget"] != manifest.DatabaseFingerprint) throw new InvalidOperationException("Explicit target confirmation is required.");
                if (action == "apply")
                {
                    var health = new Uri(new Uri(config["MediaMigration:LiveBaseUrl"] ?? throw new InvalidOperationException("Live URL required.")), "/health");
                    if (health.Scheme != "https") throw new InvalidOperationException("HTTPS live URL required.");
                    using var ready = await http.GetAsync(health);
                    if (!ready.IsSuccessStatusCode || !ready.Headers.TryGetValues("X-Media-Pipeline", out var values) || !values.Contains("s3-avif-webp-v1"))
                        throw new InvalidOperationException("The production media-aware build is not ready.");
                    foreach (var asset in manifest.Assets) await VerifyAsync(http, asset, options);
                    var current = await references.ScanAsync();
                    if (current.Any(reference => !manifest.References.Any(old => old.Table == reference.Table && old.Id == reference.Id && old.Column == reference.Column && old.Before == reference.Before)))
                        throw new InvalidOperationException("New or changed source references found. Prepare a fresh manifest before cutover.");
                }
                Console.WriteLine($"Updated media fields: {await references.ApplyAsync(manifest, action == "rollback")}");
                manifest.Applied = action == "apply"; manifest.RolledBack = action == "rollback";
            }
            else throw new InvalidOperationException("Use prepare, copy, verify, apply or rollback.");
            await MediaMigrationManifest.SaveAsync(path, manifest, default);
        }
        Console.WriteLine($"Media assets: {manifest.Assets.Count}; reference fields: {manifest.References.Count}; target fingerprint: {manifest.DatabaseFingerprint}");
        return true;
    }

    private static async Task<HashSet<string>> InventoryAsync(IConfiguration config, HttpClient http)
    {
        var cloud = config["MediaMigration:Source:CloudName"] ?? throw new InvalidOperationException("Source inventory credentials required.");
        if (!cloud.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) throw new InvalidDataException("Invalid source account.");
        var key = config["MediaMigration:Source:ApiKey"] ?? throw new InvalidOperationException("Source API key required.");
        var secret = config["MediaMigration:Source:ApiSecret"] ?? throw new InvalidOperationException("Source API secret required.");
        string? cursor = null;
        HashSet<string> urls = [with(StringComparer.Ordinal)];
        do
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.cloudinary.com/v1_1/{cloud}/resources/image/upload?prefix=jas-storefront/&max_results=500&next_cursor={Uri.EscapeDataString(cursor ?? "")}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}")));
            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var page = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            foreach (var item in page.RootElement.GetProperty("resources").EnumerateArray()) urls.Add(LegacyMediaUrls.Canonical(item.GetProperty("secure_url").GetString()!));
            cursor = page.RootElement.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
        } while (cursor is not null);
        return urls;
    }
    private static async Task VerifyAsync(HttpClient http, MediaMigrationAsset asset, S3StorageOptions options)
    {
        var url = asset.DestinationUrl ?? throw new InvalidOperationException("Asset has no destination.");
        var prefix = options.PublicBaseUrl.TrimEnd('/') + "/images/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal) || !url.EndsWith(".webp", StringComparison.Ordinal)) throw new InvalidDataException("Invalid destination.");
        string[] urls = url.Contains("/static/", StringComparison.Ordinal) ? [url, Path.ChangeExtension(url, ".avif")] : [url];
        foreach (var target in urls)
        {
            using var response = await http.GetAsync(target, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var bytes = await ReadBoundedAsync(response);
            var type = target.EndsWith(".avif", StringComparison.Ordinal) ? "image/avif" : "image/webp";
            if (response.Content.Headers.ContentType?.MediaType != type) throw new InvalidDataException("Incorrect object content type.");
            var info = new MagickImageInfo(bytes);
            if (info.Format != (type == "image/avif" ? MagickFormat.Avif : MagickFormat.WebP)) throw new InvalidDataException("Incorrect encoded format.");
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (asset.VerifiedObjects.TryGetValue(target, out var expected) && expected != hash) throw new InvalidDataException("Destination checksum mismatch.");
            asset.VerifiedObjects[target] = hash;
        }
    }
    private static async Task<bool> SyncLocalMediaAsync(WebApplication app)
    {
        var storage = app.Services.GetRequiredService<IStorageService>();
        var s3Options = app.Services.GetRequiredService<S3StorageOptions>();
        if (!s3Options.IsConfigured)
            throw new InvalidOperationException("Configure destination S3/R2 storage (Bucket, Endpoint, AccessKey, SecretKey, PublicBaseUrl) before running sync-local.");

        var env = app.Services.GetRequiredService<IWebHostEnvironment>();
        var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        Console.WriteLine($"Starting local media sync to S3/R2 ({s3Options.Bucket})...");
        var localFiles = Directory.GetFiles(webRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".avif" or ".svg";
            })
            .ToList();

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in localFiles)
        {
            var relativePath = "/" + Path.GetRelativePath(webRoot, file).Replace('\\', '/');
            var fileName = Path.GetFileName(file);
            var contentType = Path.GetExtension(file).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".avif" => "image/avif",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };

            await using var stream = File.OpenRead(file);
            var remoteUrl = await storage.UploadFileAsync(stream, fileName, contentType);
            replacements[relativePath] = remoteUrl;
            Console.WriteLine($"[SYNCED] {relativePath} -> {remoteUrl}");
        }

        // Update database records that point to local paths
        await using var dbScope = app.Services.CreateAsyncScope();
        await using var context = await dbScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        var settingsList = await context.DeliverySettings.ToListAsync();
        foreach (var ds in settingsList)
        {
            if (!string.IsNullOrEmpty(ds.BanglaQrImageUrl) && replacements.TryGetValue(ds.BanglaQrImageUrl, out var newUrl))
            {
                ds.BanglaQrImageUrl = newUrl;
            }
        }

        var products = await context.Products.ToListAsync();
        foreach (var p in products)
        {
            if (!string.IsNullOrEmpty(p.MainImageUrl) && replacements.TryGetValue(p.MainImageUrl, out var newUrl)) p.MainImageUrl = newUrl;
            if (!string.IsNullOrEmpty(p.OgImageUrl) && replacements.TryGetValue(p.OgImageUrl, out var newOg)) p.OgImageUrl = newOg;
        }

        var brands = await context.Brands.ToListAsync();
        foreach (var b in brands)
        {
            if (!string.IsNullOrEmpty(b.LogoUrl) && replacements.TryGetValue(b.LogoUrl, out var newLogo)) b.LogoUrl = newLogo;
            if (!string.IsNullOrEmpty(b.OgImageUrl) && replacements.TryGetValue(b.OgImageUrl, out var newOg)) b.OgImageUrl = newOg;
        }

        var categories = await context.Categories.ToListAsync();
        foreach (var c in categories)
        {
            if (!string.IsNullOrEmpty(c.Icon) && replacements.TryGetValue(c.Icon, out var newIcon)) c.Icon = newIcon;
            if (!string.IsNullOrEmpty(c.OgImageUrl) && replacements.TryGetValue(c.OgImageUrl, out var newOg)) c.OgImageUrl = newOg;
        }

        var slides = await context.CarouselSlides.ToListAsync();
        foreach (var s in slides)
        {
            if (!string.IsNullOrEmpty(s.ImageUrl) && replacements.TryGetValue(s.ImageUrl, out var newImg)) s.ImageUrl = newImg;
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"Local media sync complete! Synced {replacements.Count} files and updated database references.");
        return true;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength > 10 * 1024 * 1024) throw new InvalidDataException("Image exceeds migration size limit.");
        await using var source = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int length;
        while ((length = await source.ReadAsync(chunk)) != 0)
        {
            if (buffer.Length + length > 10 * 1024 * 1024) throw new InvalidDataException("Image exceeds migration size limit.");
            buffer.Write(chunk, 0, length);
        }
        return buffer.ToArray();
    }
    private sealed class MigrationUpload(S3StorageService storage, string identity) : IStorageService
    {
        public bool IsConfigured => storage.IsConfigured;
        public Task<string> UploadFileAsync(Stream stream, string name, string type, CancellationToken ct = default) => storage.UploadAssetAsync(stream, name, type, identity, ct);
        public Task DeleteFileAsync(string url, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
