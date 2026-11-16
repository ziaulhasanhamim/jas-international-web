using JASIntWeb.Tests.Integration.Infrastructure;

namespace JASIntWeb.Tests.Integration;

public class AdminPaginationIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task GetAdminPages_WithPaginationParams_AllReturnSuccess()
    {
        var endpoints = new[]
        {
            "/admin/categories?page=2",
            "/admin/products?page=2",
            "/admin/orders?page=2",
            "/admin/reviews?page=2",
            "/admin/featured-products?page=2",
            "/admin/brands?page=2",
            "/admin/inventory?page=2",
            "/admin/users?page=2",
            "/admin/coupons?page=2",
            "/admin/attributes?page=2",
            "/admin/marketing/abandoned-carts?page=2",
            "/admin/carousel"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await AdminClient.GetAsync(endpoint);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Endpoint {endpoint} failed with HTTP {(int)response.StatusCode}: {body}");
            }
            await response.IsSuccessStatusCode.Should().BeTrue();
        }
    }
}
