using System.Security.Claims;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        endpoints.MapPost("/cart/buy-now", async (
            HttpContext context,
            [FromForm] int productId,
            [FromForm] int? variantId,
            AppDbContext db,
            ShoppingService shop) =>
        {
            var vId = variantId ?? 0;
            if (vId <= 0 && productId > 0)
            {
                var defaultVariant = await db.ProductVariants.AsNoTracking()
                    .Where(v => v.ProductId == productId && !v.IsDeleted && v.IsActive)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenBy(v => v.Id)
                    .Select(v => v.Id)
                    .FirstOrDefaultAsync();
                vId = defaultVariant;
            }

            if (vId > 0)
            {
                var userId = ShoppingService.UserId(context.User);
                if (userId.HasValue)
                {
                    await shop.ChangeAsync(userId.Value, new CartLineRequest(vId, 1), true);
                }
            }

            var redirectUrl = vId > 0 ? $"/cart?added={vId}" : "/cart";
            return Results.Redirect(redirectUrl, false, false);
        });

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
