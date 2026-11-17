using JASIntWeb.Persistence;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace JASIntWeb.Tests.Integration;

public class BrandAdminUiTests : JasIntegrationTestBase
{
    [Test]
    public async Task BrandList_UsesReadableContentAndPreservesFiltersAndPagination()
    {
        using var client = AdminClient;
        var html = await client.GetStringAsync("/admin/brands?page=999&sort=Name&direction=desc");
        await html.Contains("Manage brand pages, logos and homepage visibility.").Should().BeTrue();
        await html.Contains("database QuickGrid").Should().BeFalse();
        await html.Contains("context.Name").Should().BeFalse();
        await html.Contains("/brand/anker").Should().BeTrue();
        await html.Contains("brand-mobile-card").Should().BeTrue();
        await html.Contains("Page 1 of 1").Should().BeTrue();
        var missing = await client.GetStringAsync("/admin/brands?q=missing-fixture-brand");
        await missing.Contains("No matching brands").Should().BeTrue();
        await missing.Contains("Clear filters").Should().BeTrue();
    }

    [Test]
    public async Task BrandList_ActionsRecheckSecurityStampBeforeChangingVisibility()
    {
        using var client = AdminClient;
        static FormUrlEncodedContent Command(string action) => new(new Dictionary<string, string> { ["_handler"] = "brands-table-action", [action] = "1" });
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var original = await db.Brands.AsNoTracking().SingleAsync(b => b.Id == 1);
        await client.PostAsync("/admin/brands", Command("ToggleBrandId"));
        await (await db.Brands.AsNoTracking().SingleAsync(b => b.Id == 1)).IsFeatured.Should().BeEqualTo(!original.IsFeatured);
        client.DefaultRequestHeaders.Add("X-Test-SecurityStamp", "expired");
        var denied = await client.PostAsync("/admin/brands", Command("ToggleVisibilityId"));
        await (await denied.Content.ReadAsStringAsync()).Contains("session expired").Should().BeTrue();
        await (await db.Brands.AsNoTracking().SingleAsync(b => b.Id == 1)).IsDeleted.Should().BeEqualTo(original.IsDeleted);
        client.DefaultRequestHeaders.Remove("X-Test-SecurityStamp");
        await client.PostAsync("/admin/brands", Command("ToggleVisibilityId"));
        await (await db.Brands.AsNoTracking().SingleAsync(b => b.Id == 1)).IsDeleted.Should().BeEqualTo(!original.IsDeleted);
    }
}
