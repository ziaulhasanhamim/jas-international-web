using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class AccountRegistrationIntegrationTests : JasIntegrationTestBase
{
    [Test]
    public async Task Signup_ProductionWithoutMailRejectsBeforeCreatingAccount()
    {
        await using var scope = CreateScope();
        var services = scope.ServiceProvider;
        var environment = NSubstitute.Substitute.For<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        environment.EnvironmentName = "Production";
        var users = services.GetRequiredService<UserManager<User>>();
        var registration = new AccountRegistrationService(services.GetRequiredService<JASIntWeb.Persistence.AppDbContext>(),
            users, services.GetRequiredService<EmailVerificationService>(), environment);
        await (await registration.RegisterAsync("blocked.signup@example.test", "Shopper", "Original123!") is RegistrationRejected).Should().BeTrue();
        await (await users.FindByEmailAsync("blocked.signup@example.test") is null).Should().BeTrue();
    }

    [Test]
    public async Task Signup_CodeWorksAcrossScopesAndCannotBeReplayedOrResetConfirmedAccount()
    {
        const string email = "verified.signup@example.test";
        string code;
        await using (var scope = CreateScope())
        {
            var registration = scope.ServiceProvider.GetRequiredService<AccountRegistrationService>();
            await (await registration.RegisterAsync(email, "Shopper", "Original123!") is VerificationRequired).Should().BeTrue();
            code = (await scope.ServiceProvider.GetRequiredService<EmailVerificationService>().GetDevCodeAsync(email))!;
            await code.Length.Should().BeEqualTo(6);
        }
        await using (var scope = CreateScope())
        {
            var registration = scope.ServiceProvider.GetRequiredService<AccountRegistrationService>();
            await (await registration.VerifyAsync(email, "wrong") is RegistrationRejected).Should().BeTrue();
            await (await registration.VerifyAsync(email, code) is VerifiedAccount).Should().BeTrue();
        }
        await using (var scope = CreateScope())
        {
            var registration = scope.ServiceProvider.GetRequiredService<AccountRegistrationService>();
            await (await registration.VerifyAsync(email, code) is RegistrationRejected).Should().BeTrue();
            await (await registration.RegisterAsync(email, "Attacker", "Changed123!") is RegistrationRejected).Should().BeTrue();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await users.FindByEmailAsync(email))!;
            await user.EmailConfirmed.Should().BeTrue();
            await (await users.CheckPasswordAsync(user, "Original123!")).Should().BeTrue();
            await (await users.CheckPasswordAsync(user, "Changed123!")).Should().BeFalse();
        }
    }

    [Test]
    public async Task Signup_ConcurrentVerificationHasExactlyOneWinner()
    {
        const string email = "concurrent.signup@example.test";
        string code;
        await using (var scope = CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AccountRegistrationService>().RegisterAsync(email, "Shopper", "Original123!");
            code = (await scope.ServiceProvider.GetRequiredService<EmailVerificationService>().GetDevCodeAsync(email))!;
        }
        async Task<bool> Verify()
        {
            await using var scope = CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AccountRegistrationService>().VerifyAsync(email, code) is VerifiedAccount;
        }
        var winners = await Task.WhenAll(Verify(), Verify());
        await winners.Count(success => success).Should().BeEqualTo(1);
    }
}
