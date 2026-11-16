using Microsoft.AspNetCore.Antiforgery;
using JASIntWeb.Services;
using System.ComponentModel.DataAnnotations;

namespace JASIntWeb.Endpoints;

public static class CustomerConfidenceEndpoints
{
    public static void MapCustomerConfidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/stock-notifications", async (HttpContext context, StockNotificationService notifications, IAntiforgery csrf, CancellationToken ct) =>
        {
            if (!await ValidFormAsync(context, csrf)) return Results.BadRequest("Refresh the page and try again.");
            var form = await context.Request.ReadFormAsync(ct);
            if (!int.TryParse(form["variantId"], out var variantId)) return Results.BadRequest("Choose a product option.");
            try { await notifications.SubscribeAsync(variantId, form["email"].ToString(), ct); }
            catch (ShopException error) { return Results.BadRequest(error.Message); }
            return Results.Redirect("/stock-notifications/requested");
        }).WithSummary("Request an email confirmation for a sold-out product option.");

        endpoints.MapPost("/stock-notifications/{token:guid}/{action}", async (Guid token, string action, HttpContext context, StockNotificationService notifications, IAntiforgery csrf, CancellationToken ct) =>
        {
            if (action is not ("confirm" or "unsubscribe")) return Results.NotFound();
            if (!await ValidFormAsync(context, csrf)) return Results.BadRequest("Refresh the page and try again.");
            await notifications.ChangePreferenceAsync(token, action == "confirm", ct);
            return Results.Redirect($"/stock-notifications/{token}/{action}?done=1");
        }).WithSummary("Confirm or cancel a stock alert after explicit form submission.");

        endpoints.MapPost("/api/admin/stock-notifications/{id:int}/retry", async (int id, HttpContext context, AppDbContext db, IAntiforgery csrf, CancellationToken ct) =>
        {
            if (!await ValidFormAsync(context, csrf)) return Results.BadRequest("Refresh the page and try again.");
            await db.StockNotifications.Where(x => x.Id == id && x.Status == StockNotificationStatus.Failed)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, x => x.ConfirmedAt == null ? StockNotificationStatus.Pending : StockNotificationStatus.Active)
                    .SetProperty(x => x.Attempts, 0).SetProperty(x => x.NextAttemptAt, DateTimeOffset.UtcNow)
                    .SetProperty(x => x.LastError, (string?)null), ct);
            return Results.Redirect("/admin/stock-notifications");
        }).RequireAuthorization("AdminOnly");

        endpoints.MapPost("/products/{id:int}/reviews", async (int id, HttpContext context, AppDbContext db, IAntiforgery csrf, CancellationToken ct) =>
        {
            if (!await ValidFormAsync(context, csrf)) return Results.BadRequest("Refresh the page and try again.");
            var form = await context.Request.ReadFormAsync(ct);
            var name = form["Name"].ToString().Trim();
            var email = form["Email"].ToString().Trim();
            var title = form["Title"].ToString().Trim();
            var comment = form["Comment"].ToString().Trim();
            if (name.Length is < 2 or > 100 || comment.Length is < 10 or > 3000 || title.Length > 150
                || !int.TryParse(form["Rating"], out var rating) || rating is < 1 or > 5
                || (email.Length > 0 && (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))))
                return Results.BadRequest("Enter your name, a rating from 1 to 5, and a review between 10 and 3000 characters.");
            var product = await db.Products.AsNoTracking().Where(p => p.Id == id && !p.IsDeleted && !p.Category!.IsDeleted)
                .Select(p => new { p.Slug }).SingleOrDefaultAsync(ct);
            if (product == null) return Results.NotFound();
            var userId = ShoppingService.UserId(context.User);
            var verified = userId.HasValue && await db.Orders.AnyAsync(o => o.UserId == userId && o.Status == OrderStatus.Delivered
                && o.Items.Any(i => i.ProductVariant!.ProductId == id), ct);
            db.ProductReviews.Add(new ProductReview
            {
                ProductId = id, CustomerName = name, CustomerEmail = email.Length == 0 ? null : email,
                Title = title.Length == 0 ? null : title, Comment = comment, Rating = rating,
                IsApproved = false, IsVerifiedPurchase = verified
            });
            await db.SaveChangesAsync(ct);
            return Results.Redirect($"/product/{Uri.EscapeDataString(product.Slug)}?review=submitted#reviews");
        }).WithSummary("Submit a product review for moderation.");
    }

    private static async Task<bool> ValidFormAsync(HttpContext context, IAntiforgery csrf)
    {
        if (!context.Request.HasFormContentType) return false;
        context.Response.Headers.CacheControl = "no-store";
        try { await csrf.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}
