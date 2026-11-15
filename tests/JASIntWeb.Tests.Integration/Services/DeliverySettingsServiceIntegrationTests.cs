using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration.Services;

public class DeliverySettingsServiceIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task GetSettingsAsync_ReturnsDatabaseDeliverySettings()
    {
        await using var scope = CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<DeliverySettingsService>();

        var settings = await deliveryService.GetSettingsAsync();

        await settings.Should().NotBeNull();
        await settings.HubDistrict.Should().BeEqualTo("Khulna");
    }

    [Test]
    public async Task CalculateDeliveryChargeAsync_InsideKhulna_AppliesHubFee()
    {
        await using var scope = CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<DeliverySettingsService>();

        var charge = await deliveryService.CalculateDeliveryChargeAsync("Khulna", 1000m);
        var settings = await deliveryService.GetSettingsAsync();

        await charge.Should().BeEqualTo(settings.HubCityFee);
    }

    [Test]
    public async Task CalculateDeliveryChargeAsync_OutsideKhulna_AppliesOutsideFee()
    {
        await using var scope = CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<DeliverySettingsService>();

        var charge = await deliveryService.CalculateDeliveryChargeAsync("Dhaka", 1000m);
        var settings = await deliveryService.GetSettingsAsync();

        await charge.Should().BeEqualTo(settings.OutsideFee);
    }
}
