using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Endpoints;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace JASIntWeb.Services;

public sealed class ServerCartBackend(
    IServiceScopeFactory scopes,
    AuthenticationStateProvider auth,
    IHttpContextAccessor httpContextAccessor,
    IJSRuntime js) : ICartBackend
{
    private Guid? _circuitCartToken;

    private async Task<T> RunAsync<T>(Func<ShoppingService, Guid?, int?, Task<T>> action)
    {
        await using var scope = scopes.CreateAsyncScope();
#pragma warning disable BL0013 // Cart backend queries authentication state per request
        var principal = (await auth.GetAuthenticationStateAsync()).User;
#pragma warning restore BL0013
        int? userId = ShoppingService.UserId(principal);

        var token = _circuitCartToken;
        var httpContext = httpContextAccessor.HttpContext;
        if (!token.HasValue && httpContext != null)
        {
            var (cookieToken, _) = CartEndpoints.ResolveCartIdentity(httpContext, principal);
            if (cookieToken.HasValue)
            {
                token = cookieToken;
                _circuitCartToken = token;
            }
        }

        if (!token.HasValue)
        {
            try
            {
                var cookieStr = await js.InvokeAsync<string?>("window.jasGetCartToken");
                if (!string.IsNullOrWhiteSpace(cookieStr) && Guid.TryParse(cookieStr, out var jsToken) && jsToken != Guid.Empty)
                {
                    token = jsToken;
                    _circuitCartToken = token;
                }
            }
            catch
            {
                // In SSR or when JS is unavailable, ignore gracefully
            }
        }

        try
        {
            var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
            return await action(shop, token, userId);
        }
        catch (ShopException e)
        {
            throw new CommerceException(e.Message);
        }
    }

    public Task<AccountDto> AccountAsync() => RunAsync((_, _, id) => Task.FromResult(new AccountDto(id.HasValue, null)));

    public Task<CartDto> GetAsync() => RunAsync((shop, token, id) =>
    {
        if (!token.HasValue && !id.HasValue)
        {
            return Task.FromResult(new CartDto(false, []));
        }
        return shop.GetCartAsync(token, id);
    });

    public Task<CartDto> ResolveAsync(List<CartLineRequest> items) => RunAsync((shop, _, _) => shop.ResolveAsync(items));

    public Task<CartDto> MergeAsync(CartMergeRequest request) => RunAsync((shop, _, id) =>
    {
        if (!id.HasValue) throw new CommerceException("Please sign in again.");
        return shop.MergeAsync(id.Value, request);
    });

    public Task<CartDto> ChangeAsync(CartLineRequest line, bool add) => RunAsync(async (shop, token, id) =>
    {
        if (!token.HasValue && !id.HasValue)
        {
            token = Guid.CreateVersion7();
            _circuitCartToken = token;
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null && !httpContext.Response.HasStarted)
            {
                CartEndpoints.SetCartCookie(httpContext, token.Value);
            }
        }
        var cart = await shop.ChangeAsync(token, id, line, add);
        if (cart.Token.HasValue && _circuitCartToken != cart.Token)
        {
            _circuitCartToken = cart.Token;
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null && !httpContext.Response.HasStarted)
            {
                CartEndpoints.SetCartCookie(httpContext, cart.Token.Value);
            }
        }
        return cart;
    });

    public Task<DeliveryRatesDto> RatesAsync() => RunAsync((shop, _, _) => Task.FromResult(shop.Rates));
}
