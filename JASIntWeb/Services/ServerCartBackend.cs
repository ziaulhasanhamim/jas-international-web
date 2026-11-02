using JASIntWeb.Client.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace JASIntWeb.Services;

public sealed class ServerCartBackend(IServiceScopeFactory scopes, AuthenticationStateProvider auth) : ICartBackend
{
    private async Task<T> RunAsync<T>(Func<ShoppingService, int?, Task<T>> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        var principal = (await auth.GetAuthenticationStateAsync()).User;
        int? id = ShoppingService.UserId(principal);
        try
        {
            return await action(scope.ServiceProvider.GetRequiredService<ShoppingService>(), id);
        }
        catch (ShopException e)
        {
            throw new CommerceException(e.Message);
        }
    }

    private static int RequireUser(int? id) => id ?? throw new CommerceException("Please sign in again.");

    public Task<AccountDto> AccountAsync() => RunAsync((_, id) => Task.FromResult(new AccountDto(id.HasValue, null)));

    public Task<CartDto> GetAsync() => RunAsync((shop, id) => shop.GetCartAsync(RequireUser(id)));

    public Task<CartDto> ResolveAsync(List<CartLineRequest> items) => RunAsync((shop, _) => shop.ResolveAsync(items));

    public Task<CartDto> MergeAsync(CartMergeRequest request) => RunAsync((shop, id) => shop.MergeAsync(RequireUser(id), request));

    public Task<CartDto> ChangeAsync(CartLineRequest line, bool add) => RunAsync((shop, id) => shop.ChangeAsync(RequireUser(id), line, add));

    public Task<DeliveryRatesDto> RatesAsync() => RunAsync((shop, _) => Task.FromResult(shop.Rates));
}
