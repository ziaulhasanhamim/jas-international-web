using JASIntWeb.Client.Dtos;
using JASIntWeb.Middleware;
using JASIntWeb.Services.Meta;
using Microsoft.AspNetCore.Http;

namespace JASIntWeb.Tests.Unit;

public class MetaAdsTrackingUnitTests
{
    [Test]
    public async Task NormalizeEmail_TrimsAndLowercases()
    {
        var input = "  John.Doe@Example.COM  ";
        var normalized = MetaConversionsApiService.NormalizeEmail(input);

        await normalized.Should().BeEqualTo("john.doe@example.com");
    }

    [Test]
    public async Task NormalizePhone_BangladeshNumber_Prepends88CountryCode()
    {
        var input = "01711465078";
        var normalized = MetaConversionsApiService.NormalizePhone(input);

        await normalized.Should().BeEqualTo("8801711465078");
    }

    [Test]
    public async Task NormalizePhone_AlreadyHas88_Maintains880Prefix()
    {
        var input = "+8801711465078";
        var normalized = MetaConversionsApiService.NormalizePhone(input);

        await normalized.Should().BeEqualTo("8801711465078");
    }

    [Test]
    public async Task NormalizePhone_FormattedWithSpacesAndDashes_StripsNonDigits()
    {
        var input = "+88 017-1146-5078";
        var normalized = MetaConversionsApiService.NormalizePhone(input);

        await normalized.Should().BeEqualTo("8801711465078");
    }

    [Test]
    public async Task HashSha256_ProducesValidLowercaseHex()
    {
        var email = "john.doe@example.com";
        var hash = MetaConversionsApiService.HashSha256(email);

        await hash.Length.Should().BeEqualTo(64);
        await hash.Should().BeEqualTo(hash.ToLowerInvariant());
    }

    [Test]
    public async Task SplitName_MultipleNames_SplitsFirstAndLast()
    {
        var (first, last) = MetaConversionsApiService.SplitName("Tanvir Ahmed Joy");

        await first.Should().BeEqualTo("Tanvir");
        await last.Should().BeEqualTo("Ahmed Joy");
    }

    [Test]
    public async Task SplitName_SingleName_PlacesInFirstName()
    {
        var (first, last) = MetaConversionsApiService.SplitName("Tanvir");

        await first.Should().BeEqualTo("Tanvir");
        await last.Should().BeEqualTo("");
    }

    [Test]
    public async Task MetaAttributionMiddleware_WithFbclidQuery_SetsFbcAndFbpCookiesAndItems()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?fbclid=IwAR123456789DemoFbclid&utm_campaign=winter_gadget_sale");

        var middleware = new MetaAttributionMiddleware(innerContext =>
        {
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        var attribution = context.Items[MetaAttributionMiddleware.AttributionItemKey] as MetaAttributionDto;
        await attribution.Should().NotBeNull();
        await attribution!.Fbclid.Should().BeEqualTo("IwAR123456789DemoFbclid");
        await attribution.UtmCampaign.Should().BeEqualTo("winter_gadget_sale");
        await attribution.UtmSource.Should().BeEqualTo("meta_ads"); // Inferred from fbclid

        // Verify response cookies
        var setCookies = context.Response.Headers["Set-Cookie"].ToList();
        await setCookies.Any(c => c!.Contains("_fbc=")).Should().BeTrue();
        await setCookies.Any(c => c!.Contains("_fbp=")).Should().BeTrue();
        await setCookies.Any(c => c!.Contains("jas_attribution=")).Should().BeTrue();
    }
}
