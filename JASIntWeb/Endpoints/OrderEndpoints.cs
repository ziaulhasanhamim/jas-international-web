using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/orders");

        group.MapPost("/device-status", async (DeviceOrdersQuery query, AppDbContext db) =>
        {
            if (query.Tokens is null || query.Tokens.Count == 0)
            {
                return Results.Ok(new List<DeviceOrderDto>());
            }

            var validTokens = query.Tokens.Take(50).Distinct().ToList();

            var orders = await db.Orders.AsNoTracking()
                .Where(o => validTokens.Contains(o.PublicToken))
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new DeviceOrderDto(
                    o.Id,
                    o.PublicToken,
                    o.CreatedAt,
                    o.Status.ToString(),
                    o.Total,
                    o.Items.Count,
                    o.Items.Select(i => i.ProductName + (string.IsNullOrEmpty(i.VariantName) ? "" : $" ({i.VariantName})")).ToList(),
                    o.DeliveryAddress != null ? o.DeliveryAddress.CustomerName : "Customer",
                    o.PaidAmount > 0 && o.Payments.Any(p => p.Status == Persistence.Entities.PaymentStatus.Completed)
                ))
                .ToListAsync();

            return Results.Ok(orders);
        });

        group.MapGet("/{ref}/status", async (string @ref, AppDbContext db) =>
        {
            var q = db.Orders.AsNoTracking().Include(o => o.Payments).AsQueryable();
            if (int.TryParse(@ref, out int id))
            {
                q = q.Where(o => o.Id == id);
            }
            else if (Guid.TryParse(@ref, out Guid token))
            {
                q = q.Where(o => o.PublicToken == token);
            }
            else
            {
                return Results.NotFound();
            }

            var order = await q.Select(o => new
            {
                o.Id,
                o.PublicToken,
                Status = o.Status.ToString(),
                PaidAmount = o.PaidAmount,
                IsPaymentVerified = o.PaidAmount > 0 && o.Payments.Any(p => p.Status == Persistence.Entities.PaymentStatus.Completed),
                IsCancelled = o.Status == Persistence.Entities.OrderStatus.Cancelled,
                HasSubmittedTrx = !string.IsNullOrEmpty(o.TransactionId),
                TransactionId = o.TransactionId
            }).FirstOrDefaultAsync();

            if (order is null) return Results.NotFound();
            return Results.Ok(order);
        });

        async Task HandleOrderSse(string orderRef, HttpContext context, AppDbContext db, CancellationToken ct)
        {
            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache, no-transform";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no";

            Order? order = null;
            if (int.TryParse(orderRef, out int orderId))
            {
                order = await db.Orders.AsNoTracking().Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId, ct);
            }
            else if (Guid.TryParse(orderRef, out Guid token))
            {
                order = await db.Orders.AsNoTracking().Include(o => o.Payments).FirstOrDefaultAsync(o => o.PublicToken == token, ct);
            }

            if (order == null)
            {
                await context.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(new { error = "not_found" })}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
                return;
            }

            var verified = order.PaidAmount > 0 && order.Payments.Any(p => p.Status == Persistence.Entities.PaymentStatus.Completed);
            var cancelled = order.Status == Persistence.Entities.OrderStatus.Cancelled;

            // Send initial state immediately
            await context.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(new { isPaymentVerified = verified, isCancelled = cancelled, status = order.Status.ToString(), orderId = order.Id, token = order.PublicToken })}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);

            if (verified || cancelled) return;

            // Stream updates every 1 second
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            {
                var cur = await db.Orders.AsNoTracking()
                    .Where(o => o.Id == order.Id)
                    .Select(o => new
                    {
                        o.Id,
                        o.PublicToken,
                        Status = o.Status.ToString(),
                        IsVerified = o.PaidAmount > 0 && o.Payments.Any(p => p.Status == Persistence.Entities.PaymentStatus.Completed),
                        IsCancelled = o.Status == Persistence.Entities.OrderStatus.Cancelled
                    })
                    .FirstOrDefaultAsync(ct);

                if (cur == null) break;

                if (cur.IsVerified || cur.IsCancelled)
                {
                    await context.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(new { isPaymentVerified = cur.IsVerified, isCancelled = cur.IsCancelled, status = cur.Status, orderId = cur.Id, token = cur.PublicToken })}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                    break;
                }
                else
                {
                    await context.Response.WriteAsync($": heartbeat\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }
            }
        }

        group.MapGet("/{ref}/sse", async (string @ref, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            await HandleOrderSse(@ref, context, db, ct);
        });

        group.MapGet("/payment-sse", async (HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var @ref = context.Request.Query["orderid"].ToString();
            if (string.IsNullOrWhiteSpace(@ref)) @ref = context.Request.Query["token"].ToString();
            if (string.IsNullOrWhiteSpace(@ref))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("orderid required");
                return;
            }
            await HandleOrderSse(@ref, context, db, ct);
        });

        return endpoints;
    }
}
