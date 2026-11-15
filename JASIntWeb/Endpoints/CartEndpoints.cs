using System.Security.Claims;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Endpoints;

public static class CartEndpoints
{
    public const string CartCookieName = "jas_cart_id";

    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var cart = endpoints.MapGroup("/api/cart");
        cart.MapGet("/security", Security);
        cart.MapGet("/account", Account);
        cart.MapGet("/delivery-rates", Rates);
        cart.MapGet("", Get);
        cart.MapPost("/resolve", Resolve);
        cart.MapPost("/merge", Merge).RequireAuthorization();
        cart.MapPost("", Add);
        cart.MapPut("", Update);

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

            string target = "/";
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//"))
                {
                    target = returnUrl;
                }
                else if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absUri))
                {
                    target = absUri.PathAndQuery;
                }
            }
            else if (context.Request.Headers.TryGetValue("Referer", out var referer) &&
                     Uri.TryCreate(referer.ToString(), UriKind.Absolute, out var refUri))
            {
                target = refUri.PathAndQuery;
            }

            // Add to cart must NEVER redirect to /cart - user remains on current browsing page
            if (string.IsNullOrWhiteSpace(target) || target.StartsWith("/cart", StringComparison.OrdinalIgnoreCase))
            {
                target = "/";
            }

            if (!target.Contains("cart=added", StringComparison.OrdinalIgnoreCase))
            {
                var sep = target.Contains('?') ? "&" : "?";
                target = $"{target}{sep}cart=added";
            }

            return Results.Redirect(target, false, false);
        }).DisableAntiforgery();

        return endpoints;
    }

    public static (Guid? Token, int? UserId) ResolveCartIdentity(HttpContext context, ClaimsPrincipal? user = null)
    {
        var userId = ShoppingService.UserId(user ?? context.User);
        Guid? token = null;
        if (context.Request.Cookies.TryGetValue(CartCookieName, out var cookieVal) &&
            Guid.TryParse(cookieVal, out var g) && g != Guid.Empty)
        {
            token = g;
        }
        return (token, userId);
    }

    public static (Guid Token, int? UserId) EnsureCartIdentity(HttpContext context, ClaimsPrincipal? user = null)
    {
        var (token, userId) = ResolveCartIdentity(context, user);
        if (!token.HasValue)
        {
            token = Guid.CreateVersion7();
            SetCartCookie(context, token.Value);
        }
        return (token.Value, userId);
    }

    public static void SetCartCookie(HttpContext context, Guid token)
    {
        context.Response.Cookies.Append(CartCookieName, token.ToString(), new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });
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
        var (token, userId) = EnsureCartIdentity(context);

        if (vId > 0)
        {
            var cart = await shop.ChangeAsync(token, userId, new CartLineRequest(vId, qty), add: true);
            return cart.Items.Sum(i => i.Quantity);
        }

        return await shop.GetCartItemCountAsync(token, userId);
    }

    public static AntiforgeryDto Security(HttpContext context, IAntiforgery csrf) =>
        new(csrf.GetAndStoreTokens(context).RequestToken!);

    public static AccountDto Account(ClaimsPrincipal user) =>
        new(user.Identity?.IsAuthenticated == true, user.Identity?.Name);

    public static DeliveryRatesDto Rates(ShoppingService shop) => shop.Rates;

    public static async Task<CartDto> Get(HttpContext context, ClaimsPrincipal user, ShoppingService shop)
    {
        var (token, userId) = ResolveCartIdentity(context, user);
        if (!token.HasValue && !userId.HasValue)
        {
            return new CartDto(false, []);
        }
        return await shop.GetCartAsync(token, userId);
    }

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

    public static async Task<CartDto> Add([FromBody] CartLineRequest line, HttpContext context, ClaimsPrincipal user, ShoppingService shop)
    {
        var (token, userId) = EnsureCartIdentity(context, user);
        return await shop.ChangeAsync(token, userId, line, true);
    }

    public static async Task<CartDto> Update([FromBody] CartLineRequest line, HttpContext context, ClaimsPrincipal user, ShoppingService shop)
    {
        var (token, userId) = EnsureCartIdentity(context, user);
        return await shop.ChangeAsync(token, userId, line, false);
    }
}
