using System.Net.Http.Json;
using JASIntWeb.Tests.Integration.Infrastructure;

namespace JASIntWeb.Tests.Integration;

public class OrderEndpointsIntegrationTests : JasIntegrationTestBase
{

    [Test]
    public async Task GetOrderStatus_ForNonExistentOrder_Returns404NotFound()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/orders/999999/status");

        await ((int)response.StatusCode).Should().BeEqualTo(404);
    }

    private sealed record OrderStatusDto(int Id, Guid PublicToken, string Status, decimal PaidAmount, bool IsPaymentVerified, bool IsCancelled);
}
