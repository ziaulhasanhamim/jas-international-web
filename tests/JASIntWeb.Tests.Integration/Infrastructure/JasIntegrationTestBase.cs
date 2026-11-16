using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration.Infrastructure;

[NotInParallel("database")]
public abstract class JasIntegrationTestBase
{
    protected static readonly JasWebApplicationFactory Factory = new();

    protected HttpClient AdminClient => Factory.CreateAdminClient();
    protected HttpClient CustomerClient => Factory.CreateCustomerClient();
    protected HttpClient AnonymousClient => Factory.CreateAnonymousClient();
    protected IServiceProvider Services => Factory.Services;

    protected ClaimsPrincipal AdminUser => CreatePrincipal(1, "Admin", "Admin");
    protected ClaimsPrincipal CustomerUser => CreatePrincipal(2, "Customer", "Customer");

    protected static ClaimsPrincipal CreatePrincipal(int userId, string name, string role) =>
        new(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        ], "TestAuth"));

    protected AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    [Before(Test)]
    public async Task SetupDatabaseAsync()
    {
        await PostgreSqlTestDatabase.ResetAsync();
        await JASIntWeb.Services.DeliverySettingsService.InvalidateCacheAsync(Factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>());
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JASIntWeb.Services.CatalogService>().InvalidateAllAsync();
    }
}
