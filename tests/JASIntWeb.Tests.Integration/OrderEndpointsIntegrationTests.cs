using System.Net.Http.Json;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    [Test]
    public async Task AdminOrderPage_CanViewAndSwitchStatusFromPendingToProcessing()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Status == OrderStatus.Pending);
        if (order == null)
        {
            var address = new DeliveryAddress
            {
                CustomerName = "Test Customer",
                PhoneNumber = "01711000000",
                FullAddress = "Test Address",
                City = "Khulna",
                District = "Khulna"
            };
            db.DeliveryAddresses.Add(address);
            await db.SaveChangesAsync();

            order = new Order
            {
                Status = OrderStatus.Pending,
                DeliveryAddressId = address.Id,
                Subtotal = 1000,
                DeliveryCharge = 80,
                Total = 1080
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Verify GET /admin/orders/{id} returns OK and does NOT render "was not found" (no flash)
        var getResponse = await AdminClient.GetAsync($"/admin/orders/{order.Id}");
        await getResponse.IsSuccessStatusCode.Should().BeTrue();
        var html = await getResponse.Content.ReadAsStringAsync();
        await html.Contains($"Order #{order.Id}").Should().BeTrue();
        await html.Contains($"Order #{order.Id} was not found.").Should().BeFalse();

        // Switch status from Pending to Processing
        var editForm = new OrderEditForm
        {
            Id = order.Id,
            Status = "Processing",
            CourierName = "Steadfast Courier",
            CourierTrackingId = "ST-123456",
            AdminNotes = "Packaged with fragile sticker"
        };
        await admin.ChangeOrderAsync(editForm);

        // Verify database state
        var updated = await db.Orders.Include(o => o.TimelineEvents).AsNoTracking().SingleAsync(o => o.Id == order.Id);
        await updated.Status.Should().BeEqualTo(OrderStatus.Processing);
        await updated.CourierName.Should().BeEqualTo("Steadfast Courier");
        await updated.CourierTrackingId.Should().BeEqualTo("ST-123456");
        await updated.AdminNotes.Should().BeEqualTo("Packaged with fragile sticker");
        await updated.TimelineEvents.Any(e => e.Title.Contains("Processing")).Should().BeTrue();
    }

    private sealed record OrderStatusDto(int Id, Guid PublicToken, string Status, decimal PaidAmount, bool IsPaymentVerified, bool IsCancelled);
}
