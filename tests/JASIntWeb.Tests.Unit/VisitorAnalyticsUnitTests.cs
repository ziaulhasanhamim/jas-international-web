using System;
using System.Threading.Tasks;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Services.Analytics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace JASIntWeb.Tests.Unit;

public class VisitorAnalyticsUnitTests
{
    [Test]
    public async Task IsBot_DetectsCommonBotsAndCrawlers()
    {
        var googlebot = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
        var bingbot = "Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)";
        var facebook = "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)";
        var uptime = "UptimeRobot/2.0 (http://www.uptimerobot.com/)";
        var realChrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var realIPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

        await UserAgentParser.IsBot(googlebot).Should().BeTrue();
        await UserAgentParser.IsBot(bingbot).Should().BeTrue();
        await UserAgentParser.IsBot(facebook).Should().BeTrue();
        await UserAgentParser.IsBot(uptime).Should().BeTrue();
        await UserAgentParser.IsBot(realChrome).Should().BeFalse();
        await UserAgentParser.IsBot(realIPhone).Should().BeFalse();
    }

    [Test]
    public async Task Parse_DeviceDetection_ResolvesMobileTabletDesktop()
    {
        var iPhoneUA = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";
        var iPadUA = "Mozilla/5.0 (iPad; CPU OS 16_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.5 Mobile/15E148 Safari/604.1";
        var windowsUA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var androidUA = "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

        var (deviPhone, browseriPhone, osiPhone) = UserAgentParser.Parse(iPhoneUA);
        var (deviPad, browseriPad, osiPad) = UserAgentParser.Parse(iPadUA);
        var (devWin, browserWin, osWin) = UserAgentParser.Parse(windowsUA);
        var (devAndroid, browserAndroid, osAndroid) = UserAgentParser.Parse(androidUA);

        await deviPhone.Should().BeEqualTo("Mobile");
        await osiPhone.Should().BeEqualTo("iOS");
        await browseriPhone.Should().BeEqualTo("Safari");

        await deviPad.Should().BeEqualTo("Tablet");
        await osiPad.Should().BeEqualTo("iOS");

        await devWin.Should().BeEqualTo("Desktop");
        await osWin.Should().BeEqualTo("Windows");
        await browserWin.Should().BeEqualTo("Chrome");

        await devAndroid.Should().BeEqualTo("Mobile");
        await osAndroid.Should().BeEqualTo("Android");
    }

    [Test]
    public async Task AnonymizeIp_AnonymizesIpv4AndIpv6()
    {
        var ipv4 = "103.145.24.89";
        var ipv6 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

        var anonIpv4 = UserAgentParser.AnonymizeIp(ipv4);
        var anonIpv6 = UserAgentParser.AnonymizeIp(ipv6);

        await anonIpv4.Should().BeEqualTo("103.145.24.0");
        await anonIpv6.Should().BeEqualTo("2001:0db8:85a3:0000::");
    }

    [Test]
    public async Task ExtractDomain_CategorizesStandardReferrers()
    {
        var google = "https://www.google.com/search?q=jas+international";
        var facebook = "https://m.facebook.com/ads/tracker";
        var instagram = "https://l.instagram.com/?u=https%3A%2F%2Fjasinternational.com.bd";
        var internalUrl = "https://www.jasinternational.com.bd/products";
        var direct = "";

        await UserAgentParser.ExtractDomain(google).Should().BeEqualTo("google.com");
        await UserAgentParser.ExtractDomain(facebook).Should().BeEqualTo("facebook.com");
        await UserAgentParser.ExtractDomain(instagram).Should().BeEqualTo("instagram.com");
        await UserAgentParser.ExtractDomain(internalUrl).Should().BeEqualTo("Internal");
        await UserAgentParser.ExtractDomain(direct).Should().BeEqualTo("Direct");
    }

    [Test]
    public async Task VisitorAnalyticsQueue_EnqueuesAndDrainsSuccessfully()
    {
        var queue = new VisitorAnalyticsQueue(NullLogger<VisitorAnalyticsQueue>.Instance);
        var log = new VisitorLog
        {
            VisitorId = "test_vid",
            SessionId = "test_sid",
            Path = "/product/gaming-mouse",
            Timestamp = DateTimeOffset.UtcNow
        };

        await queue.EnqueueAsync(log);

        var canRead = await queue.WaitToReadAsync();
        await canRead.Should().BeTrue();

        var readSuccess = queue.TryRead(out var readItem);
        await readSuccess.Should().BeTrue();
        await readItem!.VisitorId.Should().BeEqualTo("test_vid");
    }

    [Test]
    public async Task DeliverySettingsService_InvalidateCache_ClearsCacheEntry()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string key = "Jas_DeliverySettings_Cache";
        cache.Set(key, new DeliverySettings { HubDistrict = "Khulna" });

        var existsBefore = cache.TryGetValue(key, out _);
        await existsBefore.Should().BeTrue();

        DeliverySettingsService.InvalidateCache(cache);

        var existsAfter = cache.TryGetValue(key, out _);
        await existsAfter.Should().BeFalse();
    }
}
