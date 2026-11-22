using System.ComponentModel.DataAnnotations;
using JASIntWeb.Helpers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
namespace JASIntWeb.Services;
// Re-check identity, stamp and DB role for SSR submissions and every interactive dashboard command.

public sealed class AdminSession(IServiceScopeFactory scopes, AuthenticationStateProvider auth, IHttpContextAccessor http)
{
    public async Task<Result<T>> RunAsync<T>(Func<AdminService, int, Task<Result<T>>> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
#pragma warning disable BL0013 // Scoped admin session explicitly re-checks current security stamp per operation
        var user = await signIn.ValidateSecurityStampAsync(http.HttpContext?.User ?? (await auth.GetAuthenticationStateAsync()).User);
#pragma warning restore BL0013
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (user is null || !await users.IsInRoleAsync(user, "Admin"))
        {
            return new UnauthorizedError("Your administrator session expired. Sign in again.");
        }
        try
        {
            return await action(scope.ServiceProvider.GetRequiredService<AdminService>(), user.Id);
        }
        catch (DbUpdateException)
        {
            return new ConflictError("This record conflicts with another record or has changed. Reload and review your values.");
        }
        catch (InvalidOperationException ex)
        {
            return new ValidationError(ex.Message);
        }
        catch (ValidationException ex)
        {
            return new ValidationError(ex.Message);
        }
    }

    public async Task<Result> RunAsync(Func<AdminService, int, Task<Result>> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
#pragma warning disable BL0013 // Scoped admin session explicitly re-checks current security stamp per operation
        var user = await signIn.ValidateSecurityStampAsync(http.HttpContext?.User ?? (await auth.GetAuthenticationStateAsync()).User);
#pragma warning restore BL0013
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (user is null || !await users.IsInRoleAsync(user, "Admin"))
        {
            return new UnauthorizedError("Your administrator session expired. Sign in again.");
        }
        try
        {
            return await action(scope.ServiceProvider.GetRequiredService<AdminService>(), user.Id);
        }
        catch (DbUpdateException)
        {
            return new ConflictError("This record conflicts with another record or has changed. Reload and review your values.");
        }
        catch (InvalidOperationException ex)
        {
            return new ValidationError(ex.Message);
        }
        catch (ValidationException ex)
        {
            return new ValidationError(ex.Message);
        }
    }

    public Task<Result<T>> RunAsync<T>(Func<AdminService, int, Task<T>> action) =>
        RunAsync(async (service, id) => (Result<T>)(await action(service, id)));

    public Task<Result> RunAsync(Func<AdminService, int, Task> action) =>
        RunAsync(async (service, id) => { await action(service, id); return (Result)Success.Ok; });

    public Task<Result<T>> RunAsync<T>(Func<AdminService, Task<Result<T>>> action) =>
        RunAsync((service, _) => action(service));

    public Task<Result> RunAsync(Func<AdminService, Task<Result>> action) =>
        RunAsync((service, _) => action(service));

    public Task<Result<T>> RunAsync<T>(Func<AdminService, Task<T>> action) =>
        RunAsync(async (service, _) => (Result<T>)(await action(service)));

    public Task<Result> RunAsync(Func<AdminService, Task> action) =>
        RunAsync(async (service, _) => { await action(service); return (Result)Success.Ok; });
}