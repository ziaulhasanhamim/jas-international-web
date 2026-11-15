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

        // 100% Static SSR Enhanced Form Submission Endpoints (Zero WebSockets/WASM Overhead)
        endpoints.MapPost("/cart/buy-now", async (
            HttpContext context,
            [FromForm] int productId,
            [FromForm] int? variantId,
            [FromForm] int? quantity,
            AppDbContext db,
            ShoppingService shop) =>
        {
            var vId = variantId ?? 0;
            if (vId <= 0 && productId > 0)
            {
                vId = await db.ProductVariants.AsNoTracking()
                    .Where(v => v.ProductId == productId && !v.IsDeleted && v.IsActive)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenBy(v => v.Id)
                    .Select(v => v.Id)
                    .FirstOrDefaultAsync();
            }

            await ProcessAddToCartAsync(context, productId, vId, quantity ?? 1, db, shop);
            var redirectUrl = vId > 0 ? $"/cart?added={vId}" : "/cart";
            return Results.Redirect(redirectUrl, false, false);
        }).DisableAntiforgery();

        endpoints.MapPost("/cart/add", async (
            HttpContext context,
            [FromForm] int productId,
            [FromForm] int? variantId,
            [FromForm] int? quantity,
            [FromForm] string? returnUrl,
            AppDbContext db,
            ShoppingService shop) =>
        {
            await ProcessAddToCartAsync(context, productId, variantId, quantity ?? 1, db, shop);
            var target = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
                ? returnUrl
                : "/cart";
            var sep = target.Contains('?') ? "&" : "?";
            var redirectUrl = $"{target}{sep}cart=added";
            return Results.Redirect(redirectUrl, false, false);
        }).DisableAntiforgery();

        return endpoints;
    }

    private static async Task<int> ProcessAddToCartAsync(
        HttpContext context,
        int productId,
        int? variantId,
        int quantity,
        AppDbContext db,
        ShoppingService shop)
    {
        var vId = variantId ?? 0;
        if (vId <= 0 && productId > 0)
        {
            vId = await db.ProductVariants.AsNoTracking()
                .Where(v => v.ProductId == productId && !v.IsDeleted && v.IsActive)
                .OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.Id)
                .Select(v => v.Id)
                .FirstOrDefaultAsync();
        }

        var qty = Math.Clamp(quantity <= 0 ? 1 : quantity, 1, 99);
        var totalCount = 0;

        if (vId > 0)
        {
            var userId = ShoppingService.UserId(context.User);
            if (userId.HasValue)
            {
                await shop.ChangeAsync(userId.Value, new CartLineRequest(vId, qty), true);
                var userCart = await shop.GetCartAsync(userId.Value);
                totalCount = userCart.Items.Sum(i => i.Quantity);
            }
            else
            {
                var guestItems = GetGuestCartItems(context);
                var existing = guestItems.FirstOrDefault(i => i.VariantId == vId);
                if (existing != null)
                {
                    existing.Quantity = Math.Clamp(existing.Quantity + qty, 1, 99);
                }
                else
                {
                    guestItems.Add(new GuestCartItem { VariantId = vId, Quantity = qty });
                }

                totalCount = guestItems.Sum(i => i.Quantity);
                SetGuestCartCookies(context, guestItems, totalCount);
                return totalCount;
            }
        }
        else
        {
            var userId = ShoppingService.UserId(context.User);
            if (userId.HasValue)
            {
                var userCart = await shop.GetCartAsync(userId.Value);
                totalCount = userCart.Items.Sum(i => i.Quantity);
            }
            else
            {
                totalCount = GetGuestCartItems(context).Sum(i => i.Quantity);
            }
        }

        context.Response.Cookies.Append("jas_cart_count", totalCount.ToString(), new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });

        return totalCount;
    }

    private sealed class GuestCartItem
    {
        public int VariantId { get; set; }
        public int Quantity { get; set; }
    }

    private static List<GuestCartItem> GetGuestCartItems(HttpContext context)
    {
        try
        {
            if (context.Request.Cookies.TryGetValue("jas_guest_cart", out var json) && !string.IsNullOrWhiteSpace(json))
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<List<GuestCartItem>>(json);
                return items?.Where(i => i.VariantId > 0 && i.Quantity > 0).ToList() ?? [];
            }
        }
        catch {}
        return [];
    }

    private static void SetGuestCartCookies(HttpContext context, List<GuestCartItem> items, int totalCount)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(items);
        var cookieOpts = new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        };
        context.Response.Cookies.Append("jas_guest_cart", json, cookieOpts);
        context.Response.Cookies.Append("jas_cart_count", totalCount.ToString(), cookieOpts);
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
