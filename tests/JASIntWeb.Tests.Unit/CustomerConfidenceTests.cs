using JASIntWeb.Services;
using JASIntWeb.Middleware;
using JASIntWeb.Services.Meta;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace JASIntWeb.Tests.Unit;

public class CustomerConfidenceTests
{
    [Test]
    [Arguments("01712345678", "01712345678")]
    [Arguments("+8801712345678", "01712345678")]
    [Arguments("8801712345678", "01712345678")]
    [Arguments("01712-345678", "01712345678")]
    [Arguments("12345678", null)]
    [Arguments("017123456789", null)]
    [Arguments("01212345678", null)]
    public async Task PhoneNormalization_RequiresCompleteValidBangladeshMobile(string input, string? expected)
        => await BangladeshPhone.Normalize(input).Should().BeEqualTo(expected);

    [Test]
    public async Task Attribution_WithEssentialConsent_CreatesNoTrackingCookiesOrData()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "jas_cookie_consent=essential";
        context.Request.QueryString = new QueryString("?fbclid=test-click&utm_source=meta");
        var called = false;
        await new MetaAttributionMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context);
        await called.Should().BeTrue();
        await context.Response.Headers.SetCookie.Count.Should().BeEqualTo(0);
        await context.Items.ContainsKey(MetaAttributionMiddleware.AttributionItemKey).Should().BeFalse();
    }

    [Test]
    public async Task Attribution_DefaultWithoutExplicitDecline_CapturesAttribution()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?fbclid=test-click&utm_source=meta");
        var called = false;
        await new MetaAttributionMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context);
        await called.Should().BeTrue();
        await context.Response.Headers.SetCookie.Count.Should().BeGreaterThan(0);
        await context.Items.ContainsKey(MetaAttributionMiddleware.AttributionItemKey).Should().BeTrue();
    }

    [Test]
    public async Task ClientIp_DoesNotTrustRawForwardedHeader()
    {
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        await MetaConversionsApiService.ResolveClientIp(context).Should().BeEqualTo("192.0.2.10");
    }
}
