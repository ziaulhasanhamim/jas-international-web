using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace JASIntWeb.Services;

public sealed class NeonStorageService : IStorageService, IDisposable
{
    private readonly ILogger<NeonStorageService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly string? _accessKey;
    private readonly string? _secretKey;
    private readonly string? _serviceUrl;
    private readonly string _region;
    private readonly string _bucketName;
    private readonly AmazonS3Client? _s3Client;

    public bool IsConfigured => _s3Client is not null;

    public NeonStorageService(IConfiguration config, IWebHostEnvironment env, ILogger<NeonStorageService> logger)
    {
        _env = env;
        _logger = logger;

        _accessKey = config["AWS_ACCESS_KEY_ID"] ?? config["Storage:AccessKeyId"];
        _secretKey = config["AWS_SECRET_ACCESS_KEY"] ?? config["Storage:SecretAccessKey"];
        _serviceUrl = config["AWS_ENDPOINT_URL_S3"] ?? config["Storage:ServiceUrl"];
        _region = config["AWS_REGION"] ?? config["Storage:Region"] ?? "ap-southeast-1";
        _bucketName = config["Storage:BucketName"] ?? "jas-storefront-media";

        if (!string.IsNullOrWhiteSpace(_accessKey) &&
            !string.IsNullOrWhiteSpace(_secretKey) &&
            !string.IsNullOrWhiteSpace(_serviceUrl))
        {
            try
            {
                var s3Config = new AmazonS3Config
                {
                    ServiceURL = _serviceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = _region
                };
                var credentials = new BasicAWSCredentials(_accessKey, _secretKey);
                _s3Client = new AmazonS3Client(credentials, s3Config);
                _logger.LogInformation("Neon Object Storage initialized successfully for bucket '{Bucket}' at {Endpoint}", _bucketName, _serviceUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Neon Object Storage client; falling back to local disk storage.");
                _s3Client = null;
            }
        }
        else
        {
            _logger.LogInformation("Neon Object Storage credentials not provided; using local disk storage fallback.");
        }
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var safeBaseName = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (safeBaseName.Length > 30) safeBaseName = safeBaseName[..30];
        safeBaseName = string.Concat(safeBaseName.Where(char.IsLetterOrDigit));
        if (string.IsNullOrEmpty(safeBaseName)) safeBaseName = "media";

        var dateFolder = DateTime.UtcNow.ToString("yyyyMM");
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var objectKey = $"products/{dateFolder}/{safeBaseName}_{uniqueToken}{ext}";

        if (_s3Client is not null)
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                CannedACL = S3CannedACL.PublicRead
            };

            await _s3Client.PutObjectAsync(putRequest, ct);

            var publicUrl = $"{_serviceUrl!.TrimEnd('/')}/{_bucketName}/{objectKey}";
            _logger.LogInformation("Uploaded image to Neon Object Storage: {Url}", publicUrl);
            return publicUrl;
        }

        // Local disk fallback
        var localFolder = Path.Combine(_env.WebRootPath, "uploads", "products", dateFolder);
        Directory.CreateDirectory(localFolder);
        var localFileName = $"{safeBaseName}_{uniqueToken}{ext}";
        var localFilePath = Path.Combine(localFolder, localFileName);

        await using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, ct);

        var localUrl = $"/uploads/products/{dateFolder}/{localFileName}";
        _logger.LogInformation("Saved image to local disk fallback: {Url}", localUrl);
        return localUrl;
    }

    public async Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return;

        if (_s3Client is not null && fileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(fileUrl);
                var path = uri.AbsolutePath.TrimStart('/');
                if (path.StartsWith(_bucketName + "/", StringComparison.OrdinalIgnoreCase))
                {
                    var key = path[(_bucketName.Length + 1)..];
                    await _s3Client.DeleteObjectAsync(_bucketName, key, ct);
                    _logger.LogInformation("Deleted object from Neon bucket: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete remote storage object {Url}", fileUrl);
            }
        }
        else if (fileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var localPath = Path.Combine(_env.WebRootPath, fileUrl.TrimStart('/'));
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete local fallback file {Url}", fileUrl);
            }
        }
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
