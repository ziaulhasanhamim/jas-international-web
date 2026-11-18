using ImageMagick;

namespace JASIntWeb.Services;

public sealed class ImageOptimizerService(ILogger<ImageOptimizerService> logger) : IImageOptimizerService
{
    private static readonly SemaphoreSlim Encoders = new(2);
    public async Task<OptimizedImageSet> OptimizeImageAsync(Stream inputStream, string originalFileName,
        string? contentType = null, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await inputStream.CopyToAsync(buffer, ct);
        if (buffer.Length is 0 or > 10 * 1024 * 1024) throw new InvalidDataException("Invalid image size.");
        await Encoders.WaitAsync(ct);
        try { return await Task.Run(() => Convert(buffer.ToArray(), ct), ct); }
        finally { Encoders.Release(); }
    }

    private OptimizedImageSet Convert(byte[] bytes, CancellationToken ct)
    {
        // Ping all frames before decoding, so compressed animations cannot bypass the pixel budget.
        using var headers = new MagickImageCollection();
        headers.Ping(bytes);
        if (headers.Count is 0 or > 100 || headers.Any(frame => frame.Width > 12000 || frame.Height > 12000 || (long)frame.Width * frame.Height > 40_000_000)
            || headers.Count * headers.Max(frame => (long)Math.Max(frame.Width, frame.Page.Width)) * headers.Max(frame => (long)Math.Max(frame.Height, frame.Page.Height)) > 100_000_000)
            throw new InvalidDataException("Image exceeds the frame or pixel limit.");
        ct.ThrowIfCancellationRequested();
        using var frames = new MagickImageCollection(bytes);
        if (frames.Count > 1)
        {
            // Coalescing reconstructs partial GIF frames before resizing while retaining timing and loops.
            frames.Coalesce();
            foreach (var frame in frames) Prepare(frame, 90);
            ct.ThrowIfCancellationRequested();
            var webp = frames.ToByteArray(MagickFormat.WebP);
            logger.LogInformation("Converted animated image: {Frames} frames, {Bytes} WebP bytes", frames.Count, webp.Length);
            return new(webp, null, (int)frames[0].Width, (int)frames[0].Height);
        }
        var image = frames[0];
        Prepare(image, 82);
        var webpBytes = image.ToByteArray(MagickFormat.WebP);
        ct.ThrowIfCancellationRequested();
        image.Quality = 75;
        var avifBytes = image.ToByteArray(MagickFormat.Avif);
        logger.LogInformation("Converted static image: {Width}x{Height}, WebP {WebpBytes} bytes, AVIF {AvifBytes} bytes", image.Width, image.Height, webpBytes.Length, avifBytes.Length);
        return new(webpBytes, avifBytes, (int)image.Width, (int)image.Height);
    }

    private static void Prepare(IMagickImage<byte> image, uint quality)
    {
        image.AutoOrient();
        image.Resize(new MagickGeometry(2048, 2048) { Greater = true, IgnoreAspectRatio = false });
        image.Strip();
        image.Quality = quality;
    }
}
