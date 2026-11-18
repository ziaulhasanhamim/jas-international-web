namespace JASIntWeb.Services;

public sealed record OptimizedImageSet(byte[] WebpBytes, byte[]? AvifBytes, int Width, int Height)
{
    public bool IsAnimated => AvifBytes is null;
}

public interface IImageOptimizerService
{
    Task<OptimizedImageSet> OptimizeImageAsync(Stream inputStream, string originalFileName,
        string? contentType = null, CancellationToken ct = default);
}
