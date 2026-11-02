using System.Security.Claims;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace JASIntWeb.Endpoints;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var cart = endpoints.MapGroup("/api/cart");
        cart.MapGet("/security", Security);
        cart.MapGet("/account", Account);
        cart.MapGet("/delivery-rates", Rates);
        cart.MapGet("", Get).RequireAuthorization();
        cart.MapPost("/resolve", Resolve);
        cart.MapPost("/merge", Merge).RequireAuthorization();
        cart.MapPost("", Add).RequireAuthorization();
        cart.MapPut("", Update).RequireAuthorization();
        return endpoints;
    }

    public static AntiforgeryDto Security(HttpContext context, IAntiforgery csrf) =>
        new(csrf.GetAndStoreTokens(context).RequestToken!);

    public static AccountDto Account(ClaimsPrincipal user) =>
        new(user.Identity?.IsAuthenticated == true, user.Identity?.Name);

    public static DeliveryRatesDto Rates(ShoppingService shop) => shop.Rates;

    public static Task<CartDto> Get(ClaimsPrincipal user, ShoppingService shop) =>
        shop.GetCartAsync(ShoppingService.UserId(user)!.Value);

    public static Task<CartDto> Resolve([FromBody] List<CartLineRequest> items, ShoppingService shop) =>
        shop.ResolveAsync(items);

    public static Task<CartDto> Merge([FromBody] CartMergeRequest request, ClaimsPrincipal user, ShoppingService shop)
    {
        if (request.Items is null)
        {
            throw new BadHttpRequestException("Cart items are required.");
        }

        return shop.MergeAsync(ShoppingService.UserId(user)!.Value, request);
    }

    public static Task<CartDto> Add([FromBody] CartLineRequest line, ClaimsPrincipal user, ShoppingService shop) =>
        shop.ChangeAsync(ShoppingService.UserId(user)!.Value, line, true);

    public static Task<CartDto> Update([FromBody] CartLineRequest line, ClaimsPrincipal user, ShoppingService shop) =>
        shop.ChangeAsync(ShoppingService.UserId(user)!.Value, line, false);
}
