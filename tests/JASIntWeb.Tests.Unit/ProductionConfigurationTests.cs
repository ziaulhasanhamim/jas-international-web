using System.Security.Claims;
using JASIntWeb.Services;
using JASIntWeb.Services.Meta;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace JASIntWeb.Tests.Unit;

public class ProductionConfigurationTests
{
    [Test]
    public async Task PostgresUrl_PreservesEscapedCredentialsAndExplicitTlsOptions()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = "postgresql://shop%40user:pass%3Aword@localhost:5433/store%2Dtest?sslmode=verify-full&connect_timeout=12&application_name=store"
        }).Build();
        var parsed = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.ResolvePostgresConnectionString(config));
        await parsed.Username.Should().BeEqualTo("shop@user");
        await parsed.Password.Should().BeEqualTo("pass:word");
        await parsed.Database.Should().BeEqualTo("store-test");
        await parsed.Port.Should().BeEqualTo(5433);
        await parsed.SslMode.Should().BeEqualTo(SslMode.VerifyFull);
        await parsed.Timeout.Should().BeEqualTo(12);
        await parsed.ApplicationName.Should().BeEqualTo("store");
    }

    [Test]
    public async Task PostgresUrl_DefaultsToTlsAndStandardPort()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DB_URL"] = "postgres://user:pass@localhost/store" }).Build();
        var parsed = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.ResolvePostgresConnectionString(config));
        await parsed.Port.Should().BeEqualTo(5432);
        await parsed.SslMode.Should().BeEqualTo(SslMode.Require);
    }

    [Test]
    [Arguments(null, false)]
    [Arguments("false", false)]
    [Arguments("true", true)]
    [Arguments("unexpected", false)]
    public async Task ExternalLogin_EmailMustBeExplicitlyVerified(string? value, bool expected)
    {
        var identity = new ClaimsIdentity();
        if (value != null) identity.AddClaim(new Claim("email_verified", value));
        await ExternalLoginRules.HasVerifiedEmail(new ClaimsPrincipal(identity)).Should().BeEqualTo(expected);
    }

    [Test]
    public async Task ConversionQueue_RejectsOverflowWithoutBlockingAndRetainsQueuedEvents()
    {
        var queue = new MetaCapiQueue();
        var payload = new MetaCapiEventPayload { EventName = "Purchase", EventId = "test", UserData = new MetaUserData() };
        for (var i = 0; i < MetaCapiQueue.Capacity; i++) await queue.TryEnqueue(payload).Should().BeTrue();
        await queue.TryEnqueue(payload).Should().BeFalse();
        var overflow = queue.EnqueueAsync(payload);
        await overflow.IsCompleted.Should().BeTrue();
        var rejected = false;
        try { await overflow; } catch (InvalidOperationException) { rejected = true; }
        await rejected.Should().BeTrue();
        await queue.PendingCount.Should().BeEqualTo(MetaCapiQueue.Capacity);
    }
}
