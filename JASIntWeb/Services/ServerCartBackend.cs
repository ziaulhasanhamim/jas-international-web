using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Endpoints;
using JASIntWeb.Helpers;
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

    private async Task<Result<T>> RunAsync<T>(Func<ShoppingService, Guid?, int?, Task<Result<T>>> action)
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

        if (!token.HasValue && httpContext == null)
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

        var shop = scope.ServiceProvider.GetRequiredService<ShoppingService>();
        return await action(shop, token, userId);
    }

    public Task<Result<AccountDto>> AccountAsync() => RunAsync<AccountDto>((_, _, id) => Task.FromResult<Result<AccountDto>>(new AccountDto(id.HasValue, null)));

    public Task<Result<CartDto>> GetAsync() => RunAsync<CartDto>(async (shop, token, id) =>
    {
        if (!token.HasValue && !id.HasValue)
        {
            return new CartDto(false, []);
        }
        return await shop.GetCartAsync(token, id);
    });

    public Task<Result<CartDto>> ResolveAsync(List<CartLineRequest> items) => RunAsync<CartDto>((shop, _, _) => shop.ResolveAsync(items));

    public Task<Result<CartDto>> MergeAsync(CartMergeRequest request) => RunAsync<CartDto>(async (shop, _, id) =>
    {
        if (!id.HasValue) return new CommerceError("Please sign in again.", 401);
        return await shop.MergeAsync(id.Value, request);
    });

    public Task<Result<CartDto>> ChangeAsync(CartLineRequest line, bool add) => RunAsync<CartDto>(async (shop, token, id) =>
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
        var cartRes = await shop.ChangeAsync(token, id, line, add);
        switch (cartRes)
        {
            case CartDto cart:
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
            case Error err:
                return err;
        }
        return cartRes;
    });

    public Task<Result<DeliveryRatesDto>> RatesAsync() => RunAsync((shop, _, _) => Task.FromResult<Result<DeliveryRatesDto>>(shop.Rates));
}
