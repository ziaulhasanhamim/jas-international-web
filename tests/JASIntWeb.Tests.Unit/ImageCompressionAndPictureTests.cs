using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using ImageMagick;
using JASIntWeb.Client.Services;
using JASIntWeb.Services;
using JASIntWeb.Services.MediaMigration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JASIntWeb.Tests.Unit;

public class ImageCompressionAndPictureTests
{
    private const string Base = "https://media.example.test/jas-media";
    private const string Id = "0123456789abcdef0123456789abcdef";
    private static S3StorageOptions Options() => new() { Bucket = "jas-media", PublicBaseUrl = Base, EndpointUrl = "https://storage.example.test" };
    private static ImageOptimizerService Optimizer() => new(NullLogger<ImageOptimizerService>.Instance);

    [Test]
    public async Task ManagedImages_StaticPairAndAnimatedWebpOnly()
    {
        var url = $"{Base}/images/static/{Id}.webp";
        await ImageUrlHelper.GetAvifUrl(url).Should().BeEqualTo(url.Replace(".webp", ".avif"));
        await ImageUrlHelper.GetAutoOptimizedUrl(url.Replace(".webp", ".avif")).Should().BeEqualTo(url);
        await ImageUrlHelper.GetAvifUrl(url.Replace("/static/", "/animated/")).Should().BeNull();
        await ImageUrlHelper.GetAvifUrl("https://external.example.test/photo.png").Should().BeNull();
        await ImageUrlHelper.GetAvifUrl("/jas-logo.png").Should().BeEqualTo("/jas-logo.avif");
        await ImageUrlHelper.GetAutoOptimizedUrl("/jas-logo.png").Should().BeEqualTo("/jas-logo.webp");
        await ImageUrlHelper.GetAvifUrl("/uploads/legacy.png").Should().BeNull();
        await ImageUrlHelper.GetAvifUrl(url + "?signed=1").Should().BeNull();
    }

    [Test]
    public async Task StaticConversion_ProducesOnlyModernFormatsAndPreservesTransparency()
    {
        using var image = new MagickImage(MagickColors.Transparent, 80, 40);
        using var stream = new MemoryStream(image.ToByteArray(MagickFormat.Png));
        var result = await Optimizer().OptimizeImageAsync(stream, "alpha.png");
        using var webp = new MagickImage(result.WebpBytes);
        using var avif = new MagickImage(result.AvifBytes!);
        await webp.Format.Should().BeEqualTo(MagickFormat.WebP);
        await avif.Format.Should().BeEqualTo(MagickFormat.Avif);
        await webp.HasAlpha.Should().BeTrue();
        await avif.HasAlpha.Should().BeTrue();
        await result.Width.Should().BeEqualTo(80);
        await result.Height.Should().BeEqualTo(40);
        await result.IsAnimated.Should().BeFalse();
    }

    [Test]
    public async Task AnimatedConversion_PreservesFramesTimingAndLoopsWithoutAvif()
    {
        using var input = new MagickImageCollection();
        input.Add(new MagickImage(MagickColors.Red, 24, 24) { AnimationDelay = 15, AnimationIterations = 3 });
        input.Add(new MagickImage(MagickColors.Blue, 24, 24) { AnimationDelay = 25, AnimationIterations = 3 });
        using var stream = new MemoryStream(input.ToByteArray(MagickFormat.Gif));
        var result = await Optimizer().OptimizeImageAsync(stream, "animation.gif");
        using var output = new MagickImageCollection(result.WebpBytes);
        await result.AvifBytes.Should().BeNull();
        await output.Count.Should().BeEqualTo(2);
        await output[0].AnimationDelay.Should().BeEqualTo(15u);
        await output[1].AnimationDelay.Should().BeEqualTo(25u);
        await output[0].AnimationIterations.Should().BeEqualTo(3u);
    }

    [Test]
    public async Task Conversion_DownsizesAndRejectsExcessiveFrames()
    {
        using var image = new MagickImage(MagickColors.Green, 2400, 1200);
        using var stream = new MemoryStream(image.ToByteArray(MagickFormat.Png));
        var result = await Optimizer().OptimizeImageAsync(stream, "large.png");
        await result.Width.Should().BeEqualTo(2048);
        await result.Height.Should().BeEqualTo(1024);
        using var frames = new MagickImageCollection();
        for (var i = 0; i < 101; i++) frames.Add(new MagickImage(i % 2 == 0 ? MagickColors.Red : MagickColors.Blue, 2, 2) { AnimationDelay = 1 });
        using var animation = new MemoryStream(frames.ToByteArray(MagickFormat.Gif));
        await Assert.ThrowsAsync<InvalidDataException>(() => Optimizer().OptimizeImageAsync(animation, "too-many.gif"));
    }

    [Test]
    public async Task S3Upload_WritesPairWithContentTypesAndImmutableCaching()
    {
        var client = Substitute.For<IAmazonS3>();
        List<(string Key, string Type, string Cache)> puts = [];
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<PutObjectRequest>(); puts.Add((request.Key, request.ContentType, request.Headers.CacheControl));
            return Task.FromResult(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
        });
        var optimizer = Substitute.For<IImageOptimizerService>();
        optimizer.OptimizeImageAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new OptimizedImageSet([1, 2], [3, 4], 1, 1));
        var storage = new S3StorageService(client, Options(), optimizer, NullLogger<S3StorageService>.Instance);
        var url = await storage.UploadAssetAsync(new MemoryStream([1]), "x.png", "image/png", Id);
        await url.Should().BeEqualTo($"{Base}/images/static/{Id}.webp");
        await puts.Count.Should().BeEqualTo(2);
        await puts[0].Type.Should().BeEqualTo("image/webp");
        await puts[1].Type.Should().BeEqualTo("image/avif");
        await puts.All(put => put.Cache.Contains("immutable") && !put.Key.EndsWith(".png")).Should().BeTrue();
        await storage.DeleteFileAsync(url);
        await client.Received(2).DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>());
        await storage.DeleteFileAsync("https://foreign.example.test/" + Id + ".webp");
        await client.Received(2).DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task S3Upload_PartialFailureCleansPairAndMissingConfigDoesNotWrite()
    {
        var client = Substitute.For<IAmazonS3>();
        var optimizer = Substitute.For<IImageOptimizerService>();
        optimizer.OptimizeImageAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new OptimizedImageSet([1], [2], 1, 1));
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            call.Arg<PutObjectRequest>().Key.EndsWith(".avif") ? Task.FromException<PutObjectResponse>(new AmazonS3Exception("Fake failure")) : Task.FromResult(new PutObjectResponse()));
        var storage = new S3StorageService(client, Options(), optimizer, NullLogger<S3StorageService>.Instance);
        await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.UploadAssetAsync(new MemoryStream([1]), "x.png", "image/png", Id));
        await client.Received(2).DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>());
        var missing = new S3StorageService(client, new(), optimizer, NullLogger<S3StorageService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.UploadFileAsync(new MemoryStream([1]), "x.png", "image/png"));
        await client.Received(2).PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SvgConversion_RasterizesSafeVectorsAndRejectsExternalPaintUrls()
    {
        var client = Substitute.For<IAmazonS3>();
        List<string> types = [];
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        { types.Add(call.Arg<PutObjectRequest>().ContentType); return Task.FromResult(new PutObjectResponse()); });
        var storage = new S3StorageService(client, Options(), Optimizer(), NullLogger<S3StorageService>.Instance);
        const string safe = "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'><rect width='40' height='20' fill='blue'/></svg>";
        await ImageUploadValidator.UploadAsync(storage, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(safe)), "logo.svg", "image/svg+xml", default);
        await string.Join(",", types).Should().BeEqualTo("image/webp,image/avif");
        var unsafeSvg = safe.Replace("fill='blue'", "fill='url(#local) url(https://external.example.test/image)'");
        await Assert.ThrowsAsync<InvalidDataException>(() => ImageUploadValidator.UploadAsync(storage, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(unsafeSvg)), "logo.svg", "image/svg+xml", default));
        await types.Count.Should().BeEqualTo(2);
    }

    [Test]
    public async Task S3Animation_WritesOnlyWebpAndDeletesOnlyOwnedObjects()
    {
        var client = Substitute.For<IAmazonS3>();
        var optimizer = Substitute.For<IImageOptimizerService>();
        optimizer.OptimizeImageAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new OptimizedImageSet([1], null, 1, 1));
        var storage = new S3StorageService(client, Options(), optimizer, NullLogger<S3StorageService>.Instance);
        var url = await storage.UploadAssetAsync(new MemoryStream([1]), "x.gif", "image/gif", Id);
        await url.Should().BeEqualTo($"{Base}/images/animated/{Id}.webp");
        await client.Received(1).PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
        await storage.DeleteFileAsync(url);
        await client.Received(1).DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task S3Configuration_SupportsNeonEndpointAndNativeAwsRegion()
    {
        var options = Options(); options.AccessKeyId = "fixture"; options.SecretAccessKey = "fixture";
        using var neon = options.CreateClient();
        await ((AmazonS3Config)neon.Config).ForcePathStyle.Should().BeTrue();
        await ((AmazonS3Config)neon.Config).ServiceURL.Should().BeEqualTo(options.EndpointUrl + "/");
        options.EndpointUrl = null; options.ForcePathStyle = false;
        using var aws = options.CreateClient();
        await ((AmazonS3Config)aws.Config).RegionEndpoint.SystemName.Should().BeEqualTo("ap-southeast-1");
        await ((AmazonS3Config)aws.Config).ForcePathStyle.Should().BeFalse();
    }

    [Test]
    public async Task MigrationCheckpoint_CancellationPreservesBackupAndRetryRetainsChecksums()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jas-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "manifest.json");
        try
        {
            var manifest = new MediaMigrationManifest { DatabaseFingerprint = "fixture", DestinationBaseUrl = Base,
                Assets = [new() { SourceUrl = "https://legacy.example.test/a.png", Identity = Id, DestinationUrl = NewUrl(), VerifiedObjects = new() { [NewUrl()] = "checksum" } }] };
            await MediaMigrationManifest.SaveAsync(path, manifest, default);
            manifest.Applied = true;
            await Assert.ThrowsAsync<OperationCanceledException>(() => MediaMigrationManifest.SaveAsync(path, manifest, new CancellationToken(true)));
            var original = System.Text.Json.JsonSerializer.Deserialize<MediaMigrationManifest>(await File.ReadAllTextAsync(path))!;
            await original.Applied.Should().BeFalse();
            await MediaMigrationManifest.SaveAsync(path, manifest, default);
            var resumed = System.Text.Json.JsonSerializer.Deserialize<MediaMigrationManifest>(await File.ReadAllTextAsync(path))!;
            await resumed.Assets[0].VerifiedObjects[NewUrl()].Should().BeEqualTo("checksum");
            await resumed.Applied.Should().BeTrue();
            if (!OperatingSystem.IsWindows()) await File.GetUnixFileMode(path).Should().BeEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally { Directory.Delete(directory, true); }
        static string NewUrl() => $"{Base}/images/static/{Id}.webp";
    }

    [Test]
    public async Task MigrationUrls_RemoveNegotiationButPreserveVisualTransformsAndHtml()
    {
        const string source = "https://res.cloudinary.com/store/image/upload/f_auto,q_auto/c_fill,w_300/v123/jas-storefront/photo.png";
        const string canonical = "https://res.cloudinary.com/store/image/upload/c_fill,w_300/v123/jas-storefront/photo.png";
        await LegacyMediaUrls.Canonical(source).Should().BeEqualTo(canonical);
        var destination = $"{Base}/images/static/{Id}.webp";
        await LegacyMediaUrls.Replace($"<p>Text</p><img src=\"{source}\">", new Dictionary<string, string> { [canonical] = destination }).Should().BeEqualTo($"<p>Text</p><img src=\"{destination}\">");
        await Assert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(LegacyMediaUrls.Replace(source, new Dictionary<string, string>())));
    }
}
