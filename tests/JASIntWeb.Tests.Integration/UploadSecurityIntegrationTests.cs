using System.Net;
using System.Net.Http.Json;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JASIntWeb.Tests.Integration;

public class UploadSecurityIntegrationTests : JasIntegrationTestBase
{
    private sealed class StrictFactory : JasWebApplicationFactory { protected override bool UseRealAntiforgery => true; }
    private static readonly StrictFactory Strict = new();
    private static HttpClient Client(string? role)
    {
        var client = Strict.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role != null) { client.DefaultRequestHeaders.Add("X-Test-UserId", role == "Admin" ? "1" : "2"); client.DefaultRequestHeaders.Add("X-Test-Role", role); }
        else client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        return client;
    }
    private static MultipartFormDataContent File(string name = "fake.png", string bytes = "this is not an image")
    {
        var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(bytes)), "file", name); return form;
    }

    [Test]
    public async Task Uploads_AnonymousAndCustomersCannotReachAdminStorage()
    {
        foreach (var path in new[] { "product-images", "brand-logo", "image" })
        {
            using var anonymous = Client(null); using var customer = Client("Customer");
            var anon = await anonymous.PostAsync($"/api/admin/uploads/{path}", File());
            await ((int)anon.StatusCode).Should().BeEqualTo(302);
            var denied = await customer.PostAsync($"/api/admin/uploads/{path}", File());
            await (denied.StatusCode == HttpStatusCode.Forbidden || denied.StatusCode == HttpStatusCode.Redirect).Should().BeTrue();
        }
    }

    [Test]
    public async Task Uploads_AdminNeedsRealAntiforgeryTokenAndValidImageContents()
    {
        using var admin = Client("Admin");
        var rejected = await admin.PostAsync("/api/admin/uploads/image", File());
        await ((int)rejected.StatusCode).Should().BeEqualTo(400);
        var security = await admin.GetFromJsonAsync<Security>("/api/admin/uploads/security");
        admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN", security!.Token);
        var invalid = await admin.PostAsync("/api/admin/uploads/image", File());
        await ((int)invalid.StatusCode).Should().BeEqualTo(400);
        var message = await invalid.Content.ReadAsStringAsync();
        await message.Contains("could not be uploaded").Should().BeTrue();
        var svg = await admin.PostAsync("/api/admin/uploads/image", File("active.svg", "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"));
        await ((int)svg.StatusCode).Should().BeEqualTo(400);
    }

    [Test]
    public async Task CustomerForms_RealAntiforgeryRejectsMissingToken()
    {
        using var client = Client(null);
        var review = await client.PostAsync("/products/1/reviews", new FormUrlEncodedContent(new Dictionary<string,string> { ["Name"] = "Malicious review", ["Rating"] = "5", ["Comment"] = "This request has no security token." }));
        await ((int)review.StatusCode).Should().BeEqualTo(400);
        var subscription = await client.PostAsync("/stock-notifications", new FormUrlEncodedContent(new Dictionary<string,string> { ["variantId"] = "1", ["email"] = "test@example.test" }));
        await ((int)subscription.StatusCode).Should().BeEqualTo(400);
    }
    [Test]
    public async Task Uploads_RealTokenAllowsValidImageAndRejectsOversizedFiles()
    {
        using var admin = Client("Admin");
        var security = await admin.GetFromJsonAsync<Security>("/api/admin/uploads/security");
        admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN", security!.Token);
        using var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.White, 2, 2);
        using var valid = new MultipartFormDataContent();
        valid.Add(new ByteArrayContent(image.ToByteArray(ImageMagick.MagickFormat.Png)), "file", "valid.png");
        await ((int)(await admin.PostAsync("/api/admin/uploads/image", valid)).StatusCode).Should().BeEqualTo(200);
        using var oversized = new MultipartFormDataContent(); oversized.Add(new ByteArrayContent(new byte[5 * 1024 * 1024 + 1]), "file", "large.png");
        await ((int)(await admin.PostAsync("/api/admin/uploads/brand-logo", oversized)).StatusCode).Should().BeEqualTo(400);
    }

    private sealed record Security(string Token);
}
