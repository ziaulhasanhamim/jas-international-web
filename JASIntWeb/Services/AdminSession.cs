using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
namespace JASIntWeb.Services;
// Re-check identity, stamp and DB role for SSR submissions and every interactive dashboard command.

public sealed class AdminSession(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public async Task<T> RunAsync<T>(Func<AdminService, int, Task<T>> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var user = await signIn.ValidateSecurityStampAsync((await auth.GetAuthenticationStateAsync()).User);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (user is null || !await users.IsInRoleAsync(user, "Admin"))
        {
            throw new ShopException("Your administrator session expired. Sign in again.");
        }
        try
        {
            return await action(scope.ServiceProvider.GetRequiredService<AdminService>(), user.Id);
        }
        catch (DbUpdateException)
        {
            throw new ShopException("This record conflicts with another record or has changed. Reload and review your values.");
        }
    }

    public Task RunAsync(Func<AdminService, Task> action) => RunAsync(async (service, _) =>
        {
            await action(service);
            return true;
        });
}
