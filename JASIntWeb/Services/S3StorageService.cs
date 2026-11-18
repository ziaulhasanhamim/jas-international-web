using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;

namespace JASIntWeb.Services;

public sealed partial class S3StorageService(IAmazonS3 client, S3StorageOptions options,
    IImageOptimizerService optimizer, ILogger<S3StorageService> logger) : IStorageService
{
    public bool IsConfigured => options.IsConfigured;
    [GeneratedRegex(@"^images/(static|animated)/[a-f0-9]{32,64}\.webp$", RegexOptions.CultureInvariant)]
    private static partial Regex ManagedKey();

    public Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default) =>
        UploadAssetAsync(stream, fileName, contentType, Guid.NewGuid().ToString("N"), ct);

    // The migration supplies a deterministic identity, making retries restart-safe.
    public async Task<string> UploadAssetAsync(Stream stream, string fileName, string contentType, string identity, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Media storage is not configured.");
        if (identity.Length is not (32 or 64) || identity.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c))) throw new ArgumentException("Invalid media identity.", nameof(identity));
        var image = await optimizer.OptimizeImageAsync(stream, fileName, contentType, ct);
        var key = $"images/{(image.IsAnimated ? "animated" : "static")}/{identity}.webp";
        List<string> attempted = [];
        try
        {
            await Put(key, image.WebpBytes, "image/webp");
            if (image.AvifBytes is { } avif) await Put(Path.ChangeExtension(key, ".avif"), avif, "image/avif");
            return options.PublicBaseUrl.TrimEnd('/') + "/" + key;
        }
        catch
        {
            // Cleanup gets its own bounded token: cancellation must not leave an incomplete pair behind.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            foreach (var item in attempted)
                try { await client.DeleteObjectAsync(new() { BucketName = options.Bucket, Key = item }, cleanup.Token); }
                catch (Exception error) { logger.LogWarning(error, "Incomplete media cleanup failed for {Key}", item); }
            throw;
        }
        async Task Put(string objectKey, byte[] bytes, string type)
        {
            attempted.Add(objectKey);
            using var body = new MemoryStream(bytes, false);
            await client.PutObjectAsync(new() { BucketName = options.Bucket, Key = objectKey, InputStream = body,
                ContentType = type, AutoCloseStream = false, UseChunkEncoding = false, DisablePayloadSigning = true,
                Headers = { CacheControl = "public,max-age=31536000,immutable" } }, ct);
        }
    }

    public async Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        var prefix = options.PublicBaseUrl.TrimEnd('/') + "/";
        if (!IsConfigured || !fileUrl.StartsWith(prefix, StringComparison.Ordinal)) return;
        var key = fileUrl[prefix.Length..];
        if (!ManagedKey().IsMatch(key)) return;
        await client.DeleteObjectAsync(new() { BucketName = options.Bucket, Key = key }, ct);
        if (key.StartsWith("images/static/", StringComparison.Ordinal))
            await client.DeleteObjectAsync(new() { BucketName = options.Bucket, Key = Path.ChangeExtension(key, ".avif") }, ct);
    }
}
