using JASIntWeb.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TUnit.AspNetCore;

namespace JASIntWeb.Tests.Integration.Infrastructure;

public class JasWebApplicationFactory : TestWebApplicationFactory<Program>
{
    protected virtual bool UseRealAntiforgery => false;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        PostgreSqlTestDatabase.InitializeAsync().GetAwaiter().GetResult();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_URL"] = PostgreSqlTestDatabase.ConnectionString,
                ["Seed:Enabled"] = "false",
                ["Mail:Provider"] = "Smtp", ["Mail:Host"] = "", ["Mail:Smtp:Host"] = "",
                ["ConnectionStrings:DefaultConnection"] = PostgreSqlTestDatabase.ConnectionString
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<JASIntWeb.Services.IStorageService>();
            services.AddSingleton<JASIntWeb.Services.IStorageService, TestImageStorage>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextFactory<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContextPool<AppDbContext>(o =>
                o.UseNpgsql(PostgreSqlTestDatabase.ConnectionString)
                 .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

            services.AddDbContextFactory<AppDbContext>(o =>
                o.UseNpgsql(PostgreSqlTestDatabase.ConnectionString)
                 .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

            if (!UseRealAntiforgery)
            {
                services.RemoveAll<IAntiforgery>();
                services.AddSingleton<IAntiforgery, TestAntiforgery>();
            }
            var stockWorker = services.FirstOrDefault(s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                && s.ImplementationType == typeof(JASIntWeb.Services.StockNotificationWorker));
            if (stockWorker != null) services.Remove(stockWorker);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-Name", "Admin");
        return client;
    }

    public HttpClient CreateCustomerClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "2");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        client.DefaultRequestHeaders.Add("X-Test-Name", "Customer");
        return client;
    }

    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        return client;
    }

    public async Task ResetDatabaseAsync()
    {
        await PostgreSqlTestDatabase.ResetAsync();
    }
}

internal sealed class TestImageStorage : JASIntWeb.Services.IStorageService
{
    public bool IsConfigured => true;
    public Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default) => Task.FromResult("/uploads/" + Path.GetFileName(fileName));
    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default) => Task.CompletedTask;
}
