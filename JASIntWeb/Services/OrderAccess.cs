using System.Security.Claims;
namespace JASIntWeb.Services;

public static class OrderAccess
{
    public static IQueryable<Order> ForUser(this IQueryable<Order> orders, ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin")) return orders;
        var userId = ShoppingService.UserId(user);
        return orders.Where(o => userId.HasValue && o.UserId == userId);
    }

    public static async Task ProtectAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.StartsWith("/order/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/orders", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/payment", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/stock-notifications", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/track-order", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/webhooks", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            string? reference = null;
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0] == "order") reference = parts[1];
            if (parts.Length >= 3 && parts[0] == "api" && parts[1] == "orders") reference = parts[2];
            if (path.StartsWith("/payment", StringComparison.OrdinalIgnoreCase) || path == "/api/orders/payment-sse")
            {
                reference = context.Request.Query["orderid"].ToString();
                if (string.IsNullOrWhiteSpace(reference)) reference = context.Request.Query["token"].ToString();
            }
            if (int.TryParse(reference, out var id))
            {
                var db = context.RequestServices.GetRequiredService<AppDbContext>();
                if (!await db.Orders.AsNoTracking().ForUser(context.User).AnyAsync(o => o.Id == id, context.RequestAborted))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
            }
        }
        await next(context);
    }
}
