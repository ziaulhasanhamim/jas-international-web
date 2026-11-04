using JASIntWeb.Services;

namespace JASIntWeb.Tests.Unit;

public class CourierLogisticsTests
{
    private readonly CourierLogisticsService _courier = new();

    [Test]
    public async Task Steadfast_GeneratesCorrectTrackingUrl()
    {
        var url = _courier.GetTrackingUrl("Steadfast Courier", "ST12345678");
        await url.Should().BeEqualTo("https://steadfast.com.bd/t/ST12345678");
    }

    [Test]
    public async Task Pathao_GeneratesCorrectTrackingUrl()
    {
        var url = _courier.GetTrackingUrl("Pathao Courier", "PT-998877");
        await url.Should().BeEqualTo("https://pathao.com/courier/tracking/?consignment_id=PT-998877");
    }

    [Test]
    public async Task RedX_GeneratesCorrectTrackingUrl()
    {
        var url = _courier.GetTrackingUrl("RedX Logistics", "REDX-5544");
        await url.Should().BeEqualTo("https://redx.com.bd/track-order/?trackingId=REDX-5544");
    }

    [Test]
    public async Task EmptyTrackingId_ReturnsEmptyUrl()
    {
        var url = _courier.GetTrackingUrl("Steadfast Courier", "   ");
        await url.Should().BeEmpty();
    }

    [Test]
    public async Task CalculateCodAmount_DeductsPaidAdvanceCorrectly()
    {
        var cod = _courier.CalculateCodAmountToCollect(12500m, 150m);
        await cod.Should().BeEqualTo(12350m);
    }

    [Test]
    public async Task CalculateCodAmount_FullPayment_ReturnsZero()
    {
        var cod = _courier.CalculateCodAmountToCollect(5000m, 5000m);
        await cod.Should().BeEqualTo(0m);
    }

    [Test]
    public async Task BuildCustomerSmsText_FormatsFriendlySmsWithTracking()
    {
        var sms = _courier.BuildCustomerSmsText("Hamim", 42, "Steadfast Courier", "ST9988", "https://steadfast.com.bd/t/ST9988");
        await sms.Should().Contain("Hamim");
        await sms.Should().Contain("#42");
        await sms.Should().Contain("Steadfast Courier");
        await sms.Should().Contain("ST9988");
        await sms.Should().Contain("https://steadfast.com.bd/t/ST9988");
    }
}
