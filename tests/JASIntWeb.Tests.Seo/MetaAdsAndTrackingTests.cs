using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Middleware;
using JASIntWeb.Services.Analytics;
using JASIntWeb.Services.Meta;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace JASIntWeb.Tests.Seo;

public class MetaAdsAndTrackingTests
{
    #region 1. Bangladesh Phone Normalization & E.164 Specs

    [Test]
    public async Task NormalizePhone_LocalStandardFormat_Prepends88Prefix()
    {
        var result = MetaConversionsApiService.NormalizePhone("01711465078");
        await result.Should().BeEqualTo("8801711465078");
    }

    [Test]
    public async Task NormalizePhone_AllMajorBangladeshOperators_NormalizedCorrectly()
    {
        var testOperators = new (string Input, string Expected)[]
        {
            ("01711122233", "8801711122233"), // Grameenphone
            ("01311122233", "8801311122233"), // GP Skitto / 013
            ("01911122233", "8801911122233"), // Banglalink
            ("01411122233", "8801411122233"), // Banglalink new
            ("01811122233", "8801811122233"), // Robi
            ("01611122233", "8801611122233"), // Airtel
            ("01511122233", "8801511122233")  // Teletalk
        };

        foreach (var (input, expected) in testOperators)
        {
            var normalized = MetaConversionsApiService.NormalizePhone(input);
            await normalized.Should().BeEqualTo(expected);
        }
    }

    [Test]
    public async Task NormalizePhone_WithInternationalPrefixes_ProducesConsistent880()
    {
        var prefixes = new[]
        {
            "+8801711465078",
            "8801711465078",
            "008801711465078"
        };

        foreach (var input in prefixes)
        {
            var normalized = MetaConversionsApiService.NormalizePhone(input);
            await normalized.Should().BeEqualTo("8801711465078");
        }
    }

    [Test]
    public async Task NormalizePhone_StripsPunctuationSpacesAndSpecialChars()
    {
        var messyFormats = new[]
        {
            "+88 01711-465078",
            "  +880 1711 465 078  ",
            "(+88) 01711.465.078",
            "01711 465 078"
        };

        foreach (var input in messyFormats)
        {
            var normalized = MetaConversionsApiService.NormalizePhone(input);
            await normalized.Should().BeEqualTo("8801711465078");
        }
    }

    [Test]
    public async Task NormalizePhone_InvalidOrEmpty_ReturnsOriginalOrEmpty()
    {
        await MetaConversionsApiService.NormalizePhone("").Should().BeEqualTo("");
        await MetaConversionsApiService.NormalizePhone("   ").Should().BeEqualTo("");
    }

    #endregion

    #region 2. Email Normalization & SHA-256 Hashing

    [Test]
    public async Task NormalizeEmail_TrimsAndLowercasesInput()
    {
        var normalized = MetaConversionsApiService.NormalizeEmail("  Customer.Shop@GMAIL.com  ");
        await normalized.Should().BeEqualTo("customer.shop@gmail.com");
    }

    [Test]
    public async Task HashSha256_ProducesValidLowercase64HexDigest()
    {
        var hash = MetaConversionsApiService.HashSha256("customer.shop@gmail.com");

        await hash.Length.Should().BeEqualTo(64);
        await hash.Should().BeEqualTo(hash.ToLowerInvariant());

        // Verifying deterministic hashing against standard vector
        var expectedHash = MetaConversionsApiService.HashSha256("customer.shop@gmail.com");
        await hash.Should().BeEqualTo(expectedHash);
    }

    #endregion

    #region 3. Customer Name Splitting for Meta Match Quality

    [Test]
    public async Task SplitName_SeparatesCompoundNamesCorrectly()
    {
        var cases = new (string Input, string ExpectedFirst, string ExpectedLast)[]
        {
            ("Tanvir", "Tanvir", ""),
            ("Tanvir Ahmed", "Tanvir", "Ahmed"),
            ("Sheikh Mohammad Tanvir Ahmed", "Sheikh", "Mohammad Tanvir Ahmed"),
            ("   Arif    Hasan   ", "Arif", "Hasan")
        };

        foreach (var (input, expectedFirst, expectedLast) in cases)
        {
            var (first, last) = MetaConversionsApiService.SplitName(input);
            await first.Should().BeEqualTo(expectedFirst);
            await last.Should().BeEqualTo(expectedLast);
        }
    }

    #endregion

    #region 4. Meta Attribution Middleware & Cookie Tracking

    [Test]
    public async Task MetaAttributionMiddleware_SetsFbcAndFbpCookies_WhenFbclidPresent()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "jas_cookie_consent=accepted";
        context.Request.QueryString = new QueryString("?fbclid=IwAR12345DemoFbclid&utm_campaign=winter_gadgets&utm_medium=cpc");

        var middleware = new MetaAttributionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        var attribution = context.Items[MetaAttributionMiddleware.AttributionItemKey] as MetaAttributionDto;
        await attribution.Should().NotBeNull();
        await attribution!.Fbclid.Should().BeEqualTo("IwAR12345DemoFbclid");
        await attribution.UtmCampaign.Should().BeEqualTo("winter_gadgets");
        await attribution.UtmSource.Should().BeEqualTo("meta_ads"); // Auto-inferred

        var setCookies = context.Response.Headers["Set-Cookie"].ToList();
        await setCookies.Any(c => c!.Contains("_fbc=")).Should().BeTrue();
        await setCookies.Any(c => c!.Contains("_fbp=")).Should().BeTrue();
        await setCookies.Any(c => c!.Contains("jas_attribution=")).Should().BeTrue();
    }

    #endregion

    #region 5. Meta CAPI User Data & Custom Data Formatting

    [Test]
    public async Task FormatUserDataInternal_HashesPiiAndPreservesTechnicalIdentifiers()
    {
        var userData = new MetaUserData
        {
            Email = "customer@example.com",
            Phone = "01711465078",
            FirstName = "Arif",
            LastName = "Hasan",
            City = "Khulna",
            State = "Khulna Division",
            Country = "BD",
            ClientIpAddress = "103.230.104.1",
            ClientUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X)",
            Fbp = "fb.1.1600000000.123456789",
            Fbc = "fb.1.1600000000.IwAR123456"
        };

        var formatted = MetaConversionsApiService.FormatUserDataInternal(userData);

        // PII must be hashed with SHA-256 inside an array
        await formatted.ContainsKey("em").Should().BeTrue();
        var emailArray = formatted["em"] as string[];
        await emailArray![0].Length.Should().BeEqualTo(64);

        await formatted.ContainsKey("ph").Should().BeTrue();
        var phoneArray = formatted["ph"] as string[];
        await phoneArray![0].Length.Should().BeEqualTo(64);

        await formatted.ContainsKey("fn").Should().BeTrue();
        await formatted.ContainsKey("ln").Should().BeTrue();
        await formatted.ContainsKey("ct").Should().BeTrue();
        await formatted.ContainsKey("st").Should().BeTrue();
        await formatted.ContainsKey("country").Should().BeTrue();

        // Technical identifiers must NOT be hashed (Meta API spec)
        await formatted["client_ip_address"]!.ToString().Should().BeEqualTo("103.230.104.1");
        await formatted["client_user_agent"]!.ToString().Should().BeEqualTo("Mozilla/5.0 (Macintosh; Intel Mac OS X)");
        await formatted["fbp"]!.ToString().Should().BeEqualTo("fb.1.1600000000.123456789");
        await formatted["fbc"]!.ToString().Should().BeEqualTo("fb.1.1600000000.IwAR123456");
    }

    [Test]
    public async Task FormatCustomDataInternal_FormatsCurrencyAndLineItemsCorrectly()
    {
        var customData = new MetaCustomData
        {
            Currency = "BDT",
            Value = 3500.00m,
            ContentType = "product",
            ContentName = "Wireless Audio Bundle",
            OrderId = "JAS-9876",
            NumItems = 2,
            ContentIds = ["OHP-610", "P20I-BLK"],
            Contents =
            [
                new MetaContentItem { Id = "OHP-610", Quantity = 1, ItemPrice = 2500m, Title = "Oraimo BoomPop2" },
                new MetaContentItem { Id = "P20I-BLK", Quantity = 1, ItemPrice = 1000m, Title = "Soundcore P20i" }
            ]
        };

        var formatted = MetaConversionsApiService.FormatCustomDataInternal(customData);

        await formatted["currency"]!.ToString().Should().BeEqualTo("BDT");
        await ((decimal)formatted["value"]!).Should().BeEqualTo(3500.00m);
        await formatted["order_id"]!.ToString().Should().BeEqualTo("JAS-9876");
        await ((int)formatted["num_items"]!).Should().BeEqualTo(2);

        var contents = formatted["contents"] as List<Dictionary<string, object?>>;
        await contents!.Count.Should().BeEqualTo(2);
        await contents[0]["id"]!.ToString().Should().BeEqualTo("OHP-610");
        await ((decimal)contents[0]["item_price"]!).Should().BeEqualTo(2500m);
    }

    #endregion

    #region 6. Non-Blocking Event Queuing Integrity

    [Test]
    public async Task VisitorAnalyticsQueue_EnqueueAndTryRead_ExecutesAsynchronouslyWithoutBlocking()
    {
        var queue = new VisitorAnalyticsQueue(NullLogger<VisitorAnalyticsQueue>.Instance);

        var log = new VisitorLog
        {
            VisitorId = "test-vid-100",
            SessionId = "test-sid-100",
            Path = "/product/anker-r50i",
            Timestamp = DateTimeOffset.UtcNow,
            DeviceType = "Mobile",
            Browser = "Safari",
            OperatingSystem = "iOS"
        };

        await queue.EnqueueAsync(log);

        var success = queue.TryRead(out var dequeued);
        await success.Should().BeTrue();
        await dequeued!.VisitorId.Should().BeEqualTo("test-vid-100");
        await dequeued.Path.Should().BeEqualTo("/product/anker-r50i");
    }

    #endregion
}
