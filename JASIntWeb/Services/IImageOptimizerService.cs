namespace JASIntWeb.Services;

public sealed record OptimizedImageSet(
    byte[] PrimaryBytes,
    string PrimaryExtension,
    string PrimaryContentType,
    byte[] WebpBytes,
    byte[] AvifBytes,
    int Width,
    int Height
);

/// <summary>
/// Service responsible for automatic image compression, EXIF auto-orientation,
/// dimension normalization, and generation of modern next-gen AVIF and WebP representations.
/// </summary>
public interface IImageOptimizerService
{
    Task<OptimizedImageSet> OptimizeImageAsync(
        Stream inputStream,
        string originalFileName,
        string? contentType = null,
        CancellationToken ct = default);
}
