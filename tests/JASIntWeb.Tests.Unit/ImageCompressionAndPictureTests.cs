using JASIntWeb.Client.Services;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ImageMagick;

namespace JASIntWeb.Tests.Unit;

public class ImageCompressionAndPictureTests : IDisposable
{
    private readonly string _tempWebRoot;

    public ImageCompressionAndPictureTests()
    {
        _tempWebRoot = Path.Combine(Path.GetTempPath(), "jas_test_webroot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWebRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempWebRoot))
        {
            try { Directory.Delete(_tempWebRoot, true); } catch { /* ignore */ }
        }
    }

    [Test]
    public async Task CloudinaryStorageService_ParsesCloudinaryUrlCorrectly()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["CLOUDINARY_URL"] = "cloudinary://885657851225788:Y05Cs76XObWlxs4b_DcjAuo4Pdo@ziaulhasanhamim"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var envMock = new TestWebHostEnvironment { WebRootPath = _tempWebRoot };
        var optimizer = new ImageOptimizerService(NullLogger<ImageOptimizerService>.Instance);
        var storage = new CloudinaryStorageService(config, envMock, optimizer, NullLogger<CloudinaryStorageService>.Instance);

        await storage.IsConfigured.Should().BeTrue();

        using var testImage = new MagickImage(MagickColors.ForestGreen, 50, 50);
        using var stream = new MemoryStream();
        testImage.Write(stream, MagickFormat.Jpeg);
        stream.Position = 0;

        var uploadedUrl = await storage.UploadFileAsync(stream, "test_earbuds.jpg", "image/jpeg");
        Console.WriteLine($"UPLOADED URL: {uploadedUrl}");
        await uploadedUrl.Should().Contain("cloudinary.com");
    }

    [Test]
    public async Task ImageUrlHelper_ResolvesLocalAvifAndWebpUrls()
    {
        const string input = "/uploads/products/202611/earbuds_a1b2c3d4.jpg";

        var avif = ImageUrlHelper.GetAvifUrl(input);
        var webp = ImageUrlHelper.GetWebpUrl(input);

        await avif.Should().BeEqualTo("/uploads/products/202611/earbuds_a1b2c3d4.avif");
        await webp.Should().BeEqualTo("/uploads/products/202611/earbuds_a1b2c3d4.webp");
    }

    [Test]
    public async Task ImageUrlHelper_ResolvesCloudinaryAvifAndWebpUrls()
    {
        const string input = "https://res.cloudinary.com/jasint/image/upload/v1700000000/jas-storefront/202611/earbuds.jpg";

        var avif = ImageUrlHelper.GetAvifUrl(input);
        var webp = ImageUrlHelper.GetWebpUrl(input);

        await avif.Should().NotBeNull();
        await avif!.Contains("/upload/f_avif,q_auto/v1700000000/jas-storefront/202611/earbuds.jpg").Should().BeTrue();

        await webp.Should().NotBeNull();
        await webp!.Contains("/upload/f_webp,q_auto/v1700000000/jas-storefront/202611/earbuds.jpg").Should().BeTrue();
    }

    [Test]
    public async Task ImageUrlHelper_ResolvesUnsplashAvifAndWebpUrls()
    {
        const string input = "https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=800&q=80";

        var avif = ImageUrlHelper.GetAvifUrl(input);
        var webp = ImageUrlHelper.GetWebpUrl(input);

        await avif.Should().NotBeNull();
        await avif!.Contains("fm=avif").Should().BeTrue();

        await webp.Should().NotBeNull();
        await webp!.Contains("fm=webp").Should().BeTrue();
    }

    [Test]
    public async Task ImageUrlHelper_IgnoresSvgAndDataUris()
    {
        await ImageUrlHelper.IsVectorOrDataUri("/icons/cart.svg").Should().BeTrue();
        await ImageUrlHelper.IsVectorOrDataUri("data:image/png;base64,iVBORw0KGgoAAAANSUhEUg==").Should().BeTrue();

        await ImageUrlHelper.GetAvifUrl("/icons/cart.svg").Should().BeNull();
        await ImageUrlHelper.GetWebpUrl("/icons/cart.svg").Should().BeNull();
        await ImageUrlHelper.GetAvifUrl("data:image/png;base64,iVBORw0KGgoAAAANSUhEUg==").Should().BeNull();
    }

    [Test]
    public async Task ImageOptimizerService_GeneratesValidWebpAndAvifPayloads()
    {
        var optimizer = new ImageOptimizerService(NullLogger<ImageOptimizerService>.Instance);

        // Generate synthetic PNG test image
        using var testImage = new MagickImage(MagickColors.DodgerBlue, 200, 200);
        using var stream = new MemoryStream();
        testImage.Write(stream, MagickFormat.Png);
        stream.Position = 0;

        var result = await optimizer.OptimizeImageAsync(stream, "sample-product.png", "image/png");

        await result.Should().NotBeNull();
        await (result.PrimaryBytes.Length > 0).Should().BeTrue();
        await (result.WebpBytes.Length > 0).Should().BeTrue();
        await (result.AvifBytes.Length > 0).Should().BeTrue();
        await result.Width.Should().BeEqualTo(200);
        await result.Height.Should().BeEqualTo(200);

        // Verify that the generated bytes are valid Magick images in their respective formats
        using var readWebp = new MagickImage(result.WebpBytes);
        await (readWebp.Format == MagickFormat.WebP).Should().BeTrue();

        using var readAvif = new MagickImage(result.AvifBytes);
        await (readAvif.Format == MagickFormat.Avif).Should().BeTrue();
    }

    [Test]
    public async Task CloudinaryStorageService_LocalFallback_SavesAndDeletesAllThreeFormats()
    {
        var config = new ConfigurationBuilder().Build();
        var envMock = new TestWebHostEnvironment { WebRootPath = _tempWebRoot };
        var optimizer = new ImageOptimizerService(NullLogger<ImageOptimizerService>.Instance);
        var storage = new CloudinaryStorageService(config, envMock, optimizer, NullLogger<CloudinaryStorageService>.Instance);

        using var testImage = new MagickImage(MagickColors.ForestGreen, 150, 150);
        using var stream = new MemoryStream();
        testImage.Write(stream, MagickFormat.Jpeg);
        stream.Position = 0;

        var uploadedUrl = await storage.UploadFileAsync(stream, "test_earbuds.jpg", "image/jpeg");
        await uploadedUrl.StartsWith("/uploads/products/").Should().BeTrue();

        var primaryRelative = uploadedUrl.TrimStart('/');
        var primaryDiskPath = Path.Combine(_tempWebRoot, primaryRelative.Replace('/', Path.DirectorySeparatorChar));
        var webpDiskPath = Path.ChangeExtension(primaryDiskPath, ".webp");
        var avifDiskPath = Path.ChangeExtension(primaryDiskPath, ".avif");

        // Verify that primary, .webp, and .avif were all automatically generated and written to disk
        await File.Exists(primaryDiskPath).Should().BeTrue();
        await File.Exists(webpDiskPath).Should().BeTrue();
        await File.Exists(avifDiskPath).Should().BeTrue();

        // Now test deletion of the image set
        await storage.DeleteFileAsync(uploadedUrl);

        await File.Exists(primaryDiskPath).Should().BeFalse();
        await File.Exists(webpDiskPath).Should().BeFalse();
        await File.Exists(avifDiskPath).Should().BeFalse();
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = default!;
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "JASIntWeb";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = default!;
    }
}
