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

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Bucket)
        && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || uri.Scheme == "http")
        && (string.IsNullOrWhiteSpace(EndpointUrl) || (Uri.TryCreate(EndpointUrl, UriKind.Absolute, out var endpoint) && (endpoint.Scheme == "https" || endpoint.Scheme == "http")));

    public static S3StorageOptions Read(IConfiguration configuration)
    {
        var options = configuration.GetSection("Storage").Get<S3StorageOptions>() ?? new();

        options.Bucket = FirstNonEmpty(
            options.Bucket,
            configuration["Storage:Bucket"],
            configuration["R2_BUCKET"],
            configuration["AWS_S3_BUCKET"],
            configuration["S3_BUCKET"],
            configuration["CLOUDFLARE_R2_BUCKET"]
        ) ?? "";

        options.EndpointUrl = FirstNonEmpty(
            options.EndpointUrl,
            configuration["Storage:EndpointUrl"],
            configuration["R2_ENDPOINT"],
            configuration["AWS_ENDPOINT_URL_S3"],
            configuration["AWS_ENDPOINT_URL"],
            configuration["S3_ENDPOINT_URL"],
            configuration["CLOUDFLARE_R2_ENDPOINT"]
        );

        options.AccessKeyId = FirstNonEmpty(
            options.AccessKeyId,
            configuration["Storage:AccessKeyId"],
            configuration["R2_ACCESS_KEY_ID"],
            configuration["AWS_ACCESS_KEY_ID"],
            configuration["CLOUDFLARE_R2_ACCESS_KEY_ID"]
        );

        options.SecretAccessKey = FirstNonEmpty(
            options.SecretAccessKey,
            configuration["Storage:SecretAccessKey"],
            configuration["R2_SECRET_ACCESS_KEY"],
            configuration["AWS_SECRET_ACCESS_KEY"],
            configuration["CLOUDFLARE_R2_SECRET_ACCESS_KEY"]
        );

        options.PublicBaseUrl = FirstNonEmpty(
            options.PublicBaseUrl,
            configuration["Storage:PublicBaseUrl"],
            configuration["R2_PUBLIC_URL"],
            configuration["PUBLIC_BASE_URL"],
            configuration["S3_PUBLIC_BASE_URL"],
            configuration["CLOUDFLARE_R2_PUBLIC_URL"]
        ) ?? "";

        var explicitRegion = FirstNonEmpty(
            configuration["Storage:Region"],
            configuration["R2_REGION"],
            configuration["AWS_REGION"]
        );

        if (explicitRegion != null)
        {
            options.Region = explicitRegion;
        }
        else if (IsCloudflareR2(options.EndpointUrl))
        {
            options.Region = "auto";
        }

        if (configuration.GetSection("Storage")["ForcePathStyle"] == null)
        {
            options.ForcePathStyle = !string.IsNullOrWhiteSpace(options.EndpointUrl);
        }

        // Automatic fallback for PublicBaseUrl if using standard AWS S3 without custom CDN
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl) && !string.IsNullOrWhiteSpace(options.Bucket))
        {
            if (string.IsNullOrWhiteSpace(options.EndpointUrl))
            {
                options.PublicBaseUrl = $"https://{options.Bucket}.s3.{options.Region}.amazonaws.com";
            }
        }

        return options;
    }

    public static bool IsCloudflareR2(string? endpoint) =>
        !string.IsNullOrWhiteSpace(endpoint) && endpoint.Contains("r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return null;
    }

    public IAmazonS3 CreateClient()
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = ForcePathStyle,
            MaxErrorRetry = 3,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
        };

        if (!string.IsNullOrWhiteSpace(EndpointUrl))
        {
            config.ServiceURL = EndpointUrl;
            config.AuthenticationRegion = Region;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(Region);
        }

        if (!IsConfigured) return new AmazonS3Client(new AnonymousAWSCredentials(), config);
        return !string.IsNullOrWhiteSpace(AccessKeyId) && !string.IsNullOrWhiteSpace(SecretAccessKey)
            ? new AmazonS3Client(new BasicAWSCredentials(AccessKeyId, SecretAccessKey), config)
            : new AmazonS3Client(config);
    }
}
