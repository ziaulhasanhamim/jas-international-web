using Amazon;
using Amazon.Runtime;
using Amazon.S3;

namespace JASIntWeb.Services;

public sealed class S3StorageOptions
{
    public string Bucket { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public string? EndpointUrl { get; set; }
    public string Region { get; set; } = "ap-southeast-1";
    public bool ForcePathStyle { get; set; } = true;
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Bucket) && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (string.IsNullOrWhiteSpace(EndpointUrl) || Uri.TryCreate(EndpointUrl, UriKind.Absolute, out var endpoint) && endpoint.Scheme == "https");

    public static S3StorageOptions Read(IConfiguration configuration)
    {
        var options = configuration.GetSection("Storage").Get<S3StorageOptions>() ?? new();
        options.EndpointUrl ??= configuration["AWS_ENDPOINT_URL_S3"];
        options.AccessKeyId ??= configuration["AWS_ACCESS_KEY_ID"];
        options.SecretAccessKey ??= configuration["AWS_SECRET_ACCESS_KEY"];
        options.Region = configuration["AWS_REGION"] ?? options.Region;
        return options;
    }

    public IAmazonS3 CreateClient()
    {
        var config = new AmazonS3Config { ForcePathStyle = ForcePathStyle, MaxErrorRetry = 3,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED };
        if (!string.IsNullOrWhiteSpace(EndpointUrl)) { config.ServiceURL = EndpointUrl; config.AuthenticationRegion = Region; }
        else config.RegionEndpoint = RegionEndpoint.GetBySystemName(Region);
        if (!IsConfigured) return new AmazonS3Client(new AnonymousAWSCredentials(), config);
        return !string.IsNullOrWhiteSpace(AccessKeyId) && !string.IsNullOrWhiteSpace(SecretAccessKey)
            ? new AmazonS3Client(new BasicAWSCredentials(AccessKeyId, SecretAccessKey), config)
            : new AmazonS3Client(config);
    }
}
