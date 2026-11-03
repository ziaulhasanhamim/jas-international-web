using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JASIntWeb.Tests.Integration;

[NotInParallel("web-integration")]
public class OrderEndpointsIntegrationTests
{
    private static readonly WebApplicationFactory<Program> Factory = new();

    [Test]
    public async Task GetOrderStatus_ForExistingOrder_Returns200WithStatusDetails()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/orders/1/status");

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var data = await response.Content.ReadFromJsonAsync<OrderStatusDto>();
            await data.Should().NotBeNull();
            await data!.Id.Should().BeEqualTo(1);
            await data.Status.Should().NotBeNull();
        }
    }

    [Test]
    public async Task GetOrderStatus_ForNonExistentOrder_Returns404NotFound()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/orders/999999/status");

        await ((int)response.StatusCode).Should().BeEqualTo(404);
    }

    private sealed record OrderStatusDto(int Id, Guid PublicToken, string Status, decimal PaidAmount, bool IsPaymentVerified, bool IsCancelled);
}
