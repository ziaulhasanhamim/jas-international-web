using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration.Services;

public class CatalogServiceIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task BrandsAsync_ReturnsSeededActiveBrands()
    {
        await using var scope = CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();

        var brands = await catalog.BrandsAsync();

        await brands.Should().NotBeNull();
        await (brands.Count >= 2).Should().BeTrue();
        await brands.Any(b => b.Slug == "anker").Should().BeTrue();
        await brands.Any(b => b.Slug == "oraimo").Should().BeTrue();
    }

    [Test]
    public async Task CategoriesAsync_ReturnsActiveRootCategories()
    {
        await using var scope = CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();

        var categories = await catalog.CategoriesAsync();

        await categories.Should().NotBeNull();
        await (categories.Count >= 3).Should().BeTrue();
        await categories.Any(c => c.Slug == "chargers-power-banks").Should().BeTrue();
        await categories.Any(c => c.Slug == "smart-wearables").Should().BeTrue();
    }

    [Test]
    public async Task ProductDetailsAsync_ForExistingProduct_ReturnsDetailsWithVariantsAndStock()
    {
        await using var scope = CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();

        var product = await catalog.DetailsAsync("anker-737-power-bank");

        await product.Should().NotBeNull();
        await product!.Name.Should().BeEqualTo("Anker 737 Power Bank");
        await product.Variants.Count.Should().BeGreaterThan(0);
        await product.Variants[0].Stock.Should().BeEqualTo(50);
        await product.Variants[0].Price.Should().BeEqualTo(14500m);
    }

    [Test]
    public async Task ProductDetailsAsync_ForNonExistentProduct_ReturnsNull()
    {
        await using var scope = CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();

        var product = await catalog.DetailsAsync("non-existent-gadget-xyz");

        await product.Should().BeNull();
    }
}
