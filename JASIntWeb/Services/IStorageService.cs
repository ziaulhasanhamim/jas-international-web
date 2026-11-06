namespace JASIntWeb.Services;

public interface IStorageService
{
    bool IsConfigured { get; }
    Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteFileAsync(string fileUrl, CancellationToken ct = default);
}
