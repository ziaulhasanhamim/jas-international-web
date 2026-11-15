using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JASIntWeb.Tests.Integration;

[NotInParallel("web-integration")]
public class MetaFeedEndpointsIntegrationTests
{
    private static readonly WebApplicationFactory<Program> Factory = new();

    [Test]
    public async Task GetMetaProductsXml_ReturnsOkWithValidRssAndGoogleMerchantElements()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/feeds/meta-products.xml");

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await response.Content.Headers.ContentType!.MediaType.Should().BeEqualTo("application/xml");

        var content = await response.Content.ReadAsStringAsync();
        await content.Should().Contain("<rss");
        await content.Should().Contain("xmlns:g=\"http://base.google.com/ns/1.0\"");
        await content.Should().Contain("<channel>");
        await content.Should().Contain("<title>JAS International · Meta Advantage+ Catalog</title>");
        await content.Should().Contain("<g:id>");
        await content.Should().Contain("<g:title>");
        await content.Should().Contain("<g:link>");
        await content.Should().Contain("<g:price>");
        await content.Should().Contain("<g:availability>");
    }

    [Test]
    public async Task GetMetaProductsCsv_ReturnsOkWithValidCsvHeaders()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/feeds/meta-products.csv");

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await response.Content.Headers.ContentType!.MediaType.Should().BeEqualTo("text/csv");

        var content = await response.Content.ReadAsStringAsync();
        await content.Should().StartWith("id,title,description,availability,condition,price,link,image_link,brand,item_group_id");
    }

    [Test]
    public async Task GetGoogleMerchantXml_ReturnsOkWithShippingRules()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/feeds/google.xml");

        await ((int)response.StatusCode).Should().BeEqualTo(200);
        await response.Content.Headers.ContentType!.MediaType.Should().BeEqualTo("application/xml");

        var content = await response.Content.ReadAsStringAsync();
        await content.Should().Contain("<rss");
        await content.Should().Contain("xmlns:g=\"http://base.google.com/ns/1.0\"");
        await content.Should().Contain("JAS International · Google Merchant Center Feed");
        await content.Should().Contain("<g:shipping>");
        await content.Should().Contain("80.00 BDT");
        await content.Should().Contain("130.00 BDT");
    }

    [Test]
    public async Task GetFeedsMetaXmlAndCsv_ReturnOk()
    {
        var client = Factory.CreateClient();
        var xmlResp = await client.GetAsync("/feeds/meta.xml");
        await ((int)xmlResp.StatusCode).Should().BeEqualTo(200);

        var csvResp = await client.GetAsync("/feeds/meta.csv");
        await ((int)csvResp.StatusCode).Should().BeEqualTo(200);
    }
}
