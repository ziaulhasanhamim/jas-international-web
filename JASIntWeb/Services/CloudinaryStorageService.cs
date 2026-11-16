using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace JASIntWeb.Services;

public sealed class CloudinaryStorageService : IStorageService
{
    private readonly Cloudinary? _cloudinary;
    private readonly ILogger<CloudinaryStorageService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IImageOptimizerService _imageOptimizer;

    public bool IsConfigured => _cloudinary is not null;

    public CloudinaryStorageService(
        IConfiguration config,
        IWebHostEnvironment env,
        IImageOptimizerService imageOptimizer,
        ILogger<CloudinaryStorageService> logger)
    {
        _env = env;
        _imageOptimizer = imageOptimizer;
        _logger = logger;

        var rawUrl = config["CLOUDINARY_URL"] ?? config["Cloudinary:Url"] ?? config["Cloudinary__Url"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_URL");
        var cloudinaryUrl = rawUrl?.Trim(' ', '"', '\'');
        if (!string.IsNullOrWhiteSpace(cloudinaryUrl))
        {
            try
            {
                if (cloudinaryUrl.StartsWith("cloudinary://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(cloudinaryUrl);
                    var parts = uri.UserInfo.Split(':');
                    if (parts.Length == 2)
                    {
                        _cloudinary = new Cloudinary(new Account(uri.Host, parts[0], parts[1])) { Api = { Secure = true } };
                        _logger.LogInformation("Cloudinary media storage initialized for cloud '{CloudName}'.", uri.Host);
                        return;
                    }
                }

                _cloudinary = new Cloudinary(cloudinaryUrl) { Api = { Secure = true } };
                _logger.LogInformation("Cloudinary media storage initialized via URL.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Cloudinary from URL; checking explicit credentials.");
            }
        }

        var cloudName = config["Cloudinary:CloudName"] ?? config["CLOUDINARY_CLOUD_NAME"];
        var apiKey = config["Cloudinary:ApiKey"] ?? config["CLOUDINARY_API_KEY"];
        var apiSecret = config["Cloudinary:ApiSecret"] ?? config["CLOUDINARY_API_SECRET"];

        if (!string.IsNullOrWhiteSpace(cloudName) && !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
        {
            try
            {
                var account = new Account(cloudName.Trim(), apiKey.Trim(), apiSecret.Trim());
                _cloudinary = new Cloudinary(account);
                _cloudinary.Api.Secure = true;
                _logger.LogInformation("Cloudinary media storage initialized successfully for cloud '{CloudName}'.", cloudName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Cloudinary credentials.");
                _cloudinary = null;
            }
        }
        else
        {
            _logger.LogInformation("Cloudinary credentials not provided; using local disk storage fallback.");
        }
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var safeBaseName = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (safeBaseName.Length > 40) safeBaseName = safeBaseName[..40];
        safeBaseName = string.Concat(safeBaseName.Where(char.IsLetterOrDigit));
        if (string.IsNullOrEmpty(safeBaseName)) safeBaseName = "media";

        var dateFolder = DateTime.UtcNow.ToString("yyyyMM");
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var publicId = $"{dateFolder}/{safeBaseName}_{uniqueToken}";

        // Buffer the stream into memory upfront so we always have the complete file bytes
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, ct);
        var fileBytes = memoryStream.ToArray();

        if (_cloudinary is not null)
        {
            using var uploadStream = new MemoryStream(fileBytes);
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, uploadStream),
                PublicId = publicId,
                Folder = "jas-storefront",
                Overwrite = false,
                UniqueFilename = true,
                EagerTransforms = new List<Transformation>
                {
                    new Transformation().FetchFormat("avif").Quality("auto"),
                    new Transformation().FetchFormat("webp").Quality("auto")
                },
                EagerAsync = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams, ct);
            if (uploadResult.Error is not null)
            {
                _logger.LogWarning("Cloudinary upload failed: {Message}", uploadResult.Error.Message);
                throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            var secureUrl = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
            if (!string.IsNullOrWhiteSpace(secureUrl) && secureUrl.Contains("/image/upload/"))
            {
                secureUrl = secureUrl.Replace("/image/upload/", "/image/upload/f_auto,q_auto/");
            }
            _logger.LogInformation("Uploaded image to Cloudinary (AVIF/WebP auto-optimized): {Url}", secureUrl);
            return secureUrl!;
        }

        // Local disk fallback with automatic AVIF and WebP companions
        OptimizedImageSet? optimized = null;
        try
        {
            using var optStream = new MemoryStream(fileBytes);
            optimized = await _imageOptimizer.OptimizeImageAsync(optStream, fileName, contentType, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automatic image optimization failed for '{FileName}'. Proceeding with raw bytes fallback.", fileName);
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var localFolder = Path.Combine(webRoot, "uploads", "products", dateFolder);
        Directory.CreateDirectory(localFolder);

        if (optimized is not null)
        {
            var primaryFileName = $"{safeBaseName}_{uniqueToken}{optimized.PrimaryExtension}";
            var webpFileName = $"{safeBaseName}_{uniqueToken}.webp";
            var avifFileName = $"{safeBaseName}_{uniqueToken}.avif";

            var primaryPath = Path.Combine(localFolder, primaryFileName);
            var webpPath = Path.Combine(localFolder, webpFileName);
            var avifPath = Path.Combine(localFolder, avifFileName);

            await File.WriteAllBytesAsync(primaryPath, optimized.PrimaryBytes, ct);
            await File.WriteAllBytesAsync(webpPath, optimized.WebpBytes, ct);
            await File.WriteAllBytesAsync(avifPath, optimized.AvifBytes, ct);

            var localUrl = $"/uploads/products/{dateFolder}/{primaryFileName}";
            _logger.LogInformation("Saved optimized image set (Fallback, WebP, AVIF) to local disk: {Url}", localUrl);
            return localUrl;
        }

        // Fallback if optimization could not process the format (e.g. raw stream)
        var rawExt = Path.GetExtension(fileName).ToLowerInvariant();
        var rawFileName = $"{safeBaseName}_{uniqueToken}{rawExt}";
        var rawPath = Path.Combine(localFolder, rawFileName);

        await File.WriteAllBytesAsync(rawPath, fileBytes, ct);

        var fallbackUrl = $"/uploads/products/{dateFolder}/{rawFileName}";
        _logger.LogInformation("Saved raw image to local disk: {Url}", fallbackUrl);
        return fallbackUrl;
    }

    public async Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return;

        if (_cloudinary is not null && fileUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // Extract public id between upload/v.../ and the file extension
                var uri = new Uri(fileUrl);
                var path = uri.AbsolutePath;
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var uploadIdx = Array.IndexOf(segments, "upload");
                if (uploadIdx >= 0 && uploadIdx < segments.Length - 1)
                {
                    // Skip version segment if present (e.g. v12345678)
                    var startIdx = uploadIdx + 1;
                    if (startIdx < segments.Length && segments[startIdx].StartsWith('v') && segments[startIdx].Length > 1 && char.IsDigit(segments[startIdx][1]))
                    {
                        startIdx++;
                    }
                    var publicPath = string.Join("/", segments[startIdx..]);
                    var withoutExt = Path.ChangeExtension(publicPath, null);
                    var destroyParams = new DeletionParams(withoutExt);
                    await _cloudinary.DestroyAsync(destroyParams);
                    _logger.LogInformation("Deleted object from Cloudinary: {PublicId}", withoutExt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Cloudinary object {Url}", fileUrl);
            }
            return;
        }

        // Local disk deletion (delete primary file and companion .webp and .avif)
        if (fileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var localPath = Path.Combine(_env.WebRootPath, relativePath);
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                _logger.LogInformation("Deleted local file: {Path}", localPath);
            }

            var dir = Path.GetDirectoryName(localPath);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(localPath);
            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(nameWithoutExt))
            {
                var companionWebp = Path.Combine(dir, $"{nameWithoutExt}.webp");
                if (File.Exists(companionWebp))
                {
                    File.Delete(companionWebp);
                    _logger.LogInformation("Deleted companion WebP file: {Path}", companionWebp);
                }

                var companionAvif = Path.Combine(dir, $"{nameWithoutExt}.avif");
                if (File.Exists(companionAvif))
                {
                    File.Delete(companionAvif);
                    _logger.LogInformation("Deleted companion AVIF file: {Path}", companionAvif);
                }
            }
        }
    }
}
