using System.Security.Claims;
using JASIntWeb.Helpers;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using JASIntWeb.Services.Meta;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Endpoints;

public static class CartEndpoints
{
    public const string CartCookieName = "jas_cart_id";

    extension (IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapCartEndpoints()
        {
        var cart = endpoints.MapGroup("/api/cart").AddEndpointFilter<CartAntiforgeryFilter>();
        cart.MapGet("/security", Security);
        cart.MapGet("/account", Account);
        cart.MapGet("/delivery-rates", Rates);
        cart.MapGet("/count", async (HttpContext context, ClaimsPrincipal user, ShoppingService shop) =>
        {
            var (token, userId) = ResolveCartIdentity(context, user);
            if (!token.HasValue && !userId.HasValue)
            {
                return Results.Ok(new { count = 0 });
            }
            var count = await shop.GetCartItemCountAsync(token, userId);
            return Results.Ok(new { count });
        });
        cart.MapGet("", Get);
        cart.MapPost("/resolve", Resolve);
        cart.MapPost("/merge", Merge).RequireAuthorization();
        cart.MapPost("", Add).DisableAntiforgery();
        cart.MapPut("", Update).DisableAntiforgery();

        // 100% Static SSR Enhanced Form Submission Endpoints (Zero WebSockets/WASM Overhead)
        endpoints.MapPost("/cart/buy-now", async (
            HttpContext context,
            [FromForm] int productId,
            [FromForm] int? variantId,
            [FromForm] int? quantity,
            AppDbContext db,
            ShoppingService shop,
            IMetaConversionsApiService metaApi) =>
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

            await ProcessAddToCartAsync(context, productId, vId, quantity ?? 1, db, shop, metaApi);
            return Results.Redirect("/cart", false, false);
        }).DisableAntiforgery().AddEndpointFilter<CartAntiforgeryFilter>();

        endpoints.MapPost("/cart/add", async (
            HttpContext context,
            [FromForm] int productId,
            [FromForm] int? variantId,
            [FromForm] int? quantity,
            [FromForm] string? returnUrl,
            AppDbContext db,
            ShoppingService shop,
            IMetaConversionsApiService metaApi) =>
        {
            await ProcessAddToCartAsync(context, productId, variantId, quantity ?? 1, db, shop, metaApi);

            var target = AccountNavigation.SafeReturn(returnUrl ?? "/");

            // Add to cart must NEVER redirect to /cart - user remains on current browsing page
            if (string.IsNullOrWhiteSpace(target) || target.StartsWith("/cart", StringComparison.OrdinalIgnoreCase))
            {
                target = "/";
            }

            // Pure POST without query parameter pollution
            if (target.Contains("cart=added"))
            {
                target = target.Replace("?cart=added", "").Replace("&cart=added", "");
                if (string.IsNullOrWhiteSpace(target)) target = "/";
            }

            return Results.LocalRedirect(target);
        }).DisableAntiforgery().AddEndpointFilter<CartAntiforgeryFilter>();

        endpoints.MapPost("/cart/update", async (
            HttpContext context,
            [FromForm] int variantId,
            [FromForm] int quantity,
            ShoppingService shop) =>
        {
            var (token, userId) = EnsureCartIdentity(context);
            if (variantId > 0)
            {
                var changeRes = await shop.ChangeAsync(token, userId, new CartLineRequest(variantId, Math.Clamp(quantity, 0, 99)), add: false);
                if (changeRes switch { CartDto => false, Error => true })
                {
                    changeRes.TryGetError(out var err);
                    return Results.Redirect($"/cart?error={Uri.EscapeDataString(err!.Message)}", false, false);
                }
            }
            return Results.Redirect("/cart", false, false);
        }).DisableAntiforgery().AddEndpointFilter<CartAntiforgeryFilter>();

        return endpoints;
    }
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
            Secure = context.Request.IsHttps,
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
        ShoppingService shop,
        IMetaConversionsApiService? metaApi = null)
    {
        var result = await AddToCartFromSsrAsync(context, productId, variantId, quantity, db, shop, metaApi);
        if (result is CartDto cart)
        {
            return cart.Items.Sum(i => i.Quantity);
        }
        var (token, userId) = EnsureCartIdentity(context);
        return await shop.GetCartItemCountAsync(token, userId);
    }

    internal static async Task<Result<CartDto>> AddToCartFromSsrAsync(
        HttpContext context,
        int productId,
        int? variantId,
        int quantity,
        AppDbContext db,
        ShoppingService shop,
        IMetaConversionsApiService? metaApi = null)
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

        if (vId <= 0)
        {
            return Error.Failure("Selected product option is unavailable.");
        }

        var cartRes = await shop.ChangeAsync(token, userId, new CartLineRequest(vId, qty), add: true);
        switch (cartRes)
        {
            case CartDto cart:
                if (cart.Token.HasValue && cart.Token.Value != token)
                {
                    SetCartCookie(context, cart.Token.Value);
                }

                if (metaApi != null)
                {
                    var variant = await db.ProductVariants.AsNoTracking()
                        .Include(v => v.Product).ThenInclude(p => p!.Category)
                        .FirstOrDefaultAsync(v => v.Id == vId);
                    if (variant != null)
                    {
                        var sku = (!string.IsNullOrWhiteSpace(variant.SKU) ? variant.SKU : $"V{variant.Id}").Trim().ToUpperInvariant();
                        var atcEventId = $"atc_{variant.Id}_{Guid.CreateVersion7():N}";
                        await metaApi.EnqueueAddToCartEventAsync(sku, variant.Product?.Name ?? "Product", variant.Product?.Category?.Name, variant.SellingPrice, qty, atcEventId, context);
                    }
                }

                return cart;
            case Error err:
                return err;
            default:
                return Error.Failure("Unable to add product to cart.");
        }
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
        var cart = await shop.GetCartAsync(token, userId);
        if (cart.Token.HasValue && (!token.HasValue || cart.Token.Value != token.Value))
        {
            SetCartCookie(context, cart.Token.Value);
        }
        return cart;
    }

    public static async Task<IResult> Resolve([FromBody] List<CartLineRequest> items, ShoppingService shop)
    {
        var result = await shop.ResolveAsync(items);
        return result.ToHttpResult();
    }

    public static async Task<IResult> Merge([FromBody] CartMergeRequest request, ClaimsPrincipal user, ShoppingService shop)
    {
        if (request.Items is null)
        {
            throw new BadHttpRequestException("Cart items are required.");
        }

        var result = await shop.MergeAsync(ShoppingService.UserId(user)!.Value, request);
        return result.ToHttpResult();
    }

    public static async Task<IResult> Add(
        [FromBody] CartLineRequest line,
        HttpContext context,
        ClaimsPrincipal user,
        ShoppingService shop,
        AppDbContext db,
        IMetaConversionsApiService metaApi)
    {
        var variantId = line.VariantId;
        if (variantId <= 0 && line.ProductId > 0)
        {
            variantId = await db.ProductVariants.AsNoTracking()
                .Where(v => v.ProductId == line.ProductId && !v.IsDeleted && v.IsActive)
                .OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.Id)
                .Select(v => v.Id)
                .FirstOrDefaultAsync();
        }

        if (variantId <= 0)
        {
            return Results.BadRequest(new ApiErrorDto("A valid product variant is required.", "invalid_variant"));
        }

        var (token, userId) = EnsureCartIdentity(context, user);
        var changeRes = await shop.ChangeAsync(token, userId, new CartLineRequest(variantId, line.Quantity, line.ProductId), true);
        if (changeRes switch { CartDto => false, Error => true })
        {
            changeRes.TryGet(out _, out var err);
            return Results.BadRequest(new ApiErrorDto(err!.Message, "shop_error"));
        }
        var cart = changeRes.ValueOrDefault()!;
        if (cart.Token.HasValue && cart.Token.Value != token)
        {
            SetCartCookie(context, cart.Token.Value);
        }

        var variant = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p!.Category)
            .FirstOrDefaultAsync(v => v.Id == variantId);

        string? atcEventId = null;
        string? sku = null;
        if (variant != null)
        {
            sku = (!string.IsNullOrWhiteSpace(variant.SKU) ? variant.SKU : $"V{variant.Id}").Trim().ToUpperInvariant();
            atcEventId = $"atc_{variant.Id}_{Guid.CreateVersion7():N}";
            var qty = line.Quantity > 0 ? line.Quantity : 1;
            await metaApi.EnqueueAddToCartEventAsync(sku, variant.Product?.Name ?? "Product", variant.Product?.Category?.Name, variant.SellingPrice, qty, atcEventId, context);

            context.Response.Headers["X-Meta-Event-Id"] = atcEventId;
            context.Response.Headers["X-Meta-SKU"] = sku;

            cart = cart with { MetaEventId = atcEventId, AddedSku = sku };
        }

        return Results.Ok(cart);
    }

    public static async Task<IResult> Update(
        [FromBody] CartLineRequest line,
        HttpContext context,
        ClaimsPrincipal user,
        ShoppingService shop)
    {
        var (token, userId) = EnsureCartIdentity(context, user);
        var changeRes = await shop.ChangeAsync(token, userId, line, false);
        if (changeRes switch { CartDto => false, Error => true })
        {
            changeRes.TryGet(out _, out var err);
            return Results.BadRequest(new ApiErrorDto(err!.Message, "shop_error"));
        }
        var cart = changeRes.ValueOrDefault()!;
        if (cart.Token.HasValue && cart.Token.Value != token)
        {
            SetCartCookie(context, cart.Token.Value);
        }
        return Results.Ok(cart);
    }
}
