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
}
