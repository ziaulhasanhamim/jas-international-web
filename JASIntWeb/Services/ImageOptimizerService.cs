using ImageMagick;

namespace JASIntWeb.Services;

public sealed class ImageOptimizerService : IImageOptimizerService
{
    private readonly ILogger<ImageOptimizerService> _logger;

    public ImageOptimizerService(ILogger<ImageOptimizerService> logger)
    {
        _logger = logger;
    }

    public async Task<OptimizedImageSet> OptimizeImageAsync(
        Stream inputStream,
        string originalFileName,
        string? contentType = null,
        CancellationToken ct = default)
    {
        var originalExt = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (originalExt == ".svg" || contentType == "image/svg+xml")
        {
            throw new NotSupportedException("SVG images are vector assets and do not require raster optimization.");
        }

        using var memoryStream = new MemoryStream();
        await inputStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var image = new MagickImage(memoryStream);

            // Auto-orient based on EXIF tags (e.g. photos taken vertically on smartphones)
            image.AutoOrient();

            // Downscale oversized images (keep aspect ratio, max 2048x2048 for high-res e-commerce)
            const int maxDimension = 2048;
            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                var geometry = new MagickGeometry(maxDimension, maxDimension)
                {
                    IgnoreAspectRatio = false,
                    Greater = true
                };
                image.Resize(geometry);
            }

            // Strip metadata profiles (GPS, camera tags, thumbnails) to shrink payload
            image.Strip();

            var originalExt = Path.GetExtension(originalFileName).ToLowerInvariant();
            var hasAlpha = image.HasAlpha;

            // 1. Generate WebP
            using var webpImage = image.Clone();
            webpImage.Format = MagickFormat.WebP;
            webpImage.Quality = 82;
            var webpBytes = webpImage.ToByteArray();

            // 2. Generate AVIF (Next-Gen high compression)
            using var avifImage = image.Clone();
            avifImage.Format = MagickFormat.Avif;
            avifImage.Quality = 75;
            var avifBytes = avifImage.ToByteArray();

            // 3. Generate Optimized Primary Fallback
            string primaryExt;
            string primaryContentType;

            if (hasAlpha || originalExt == ".png")
            {
                image.Format = MagickFormat.Png;
                image.Quality = 90;
                primaryExt = ".png";
                primaryContentType = "image/png";
            }
            else
            {
                image.Format = MagickFormat.Jpeg;
                image.Quality = 85;
                primaryExt = ".jpg";
                primaryContentType = "image/jpeg";
            }

            var primaryBytes = image.ToByteArray();

            _logger.LogInformation(
                "Optimized image '{FileName}' ({Width}x{Height}): Fallback {FallbackKB:F1}KB, WebP {WebpKB:F1}KB, AVIF {AvifKB:F1}KB",
                originalFileName,
                image.Width,
                image.Height,
                primaryBytes.Length / 1024.0,
                webpBytes.Length / 1024.0,
                avifBytes.Length / 1024.0);

            return new OptimizedImageSet(
                PrimaryBytes: primaryBytes,
                PrimaryExtension: primaryExt,
                PrimaryContentType: primaryContentType,
                WebpBytes: webpBytes,
                AvifBytes: avifBytes,
                Width: (int)image.Width,
                Height: (int)image.Height);
        }, ct);
    }
}
