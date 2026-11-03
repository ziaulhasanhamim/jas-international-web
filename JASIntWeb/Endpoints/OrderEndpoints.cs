using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Endpoints;

public sealed record OrderPaymentSseDto(
    int OrderId,
    Guid PublicToken,
    bool IsPaymentVerified,
    bool IsCancelled,
    string Status);

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
                    o.PaidAmount > 0 && o.Payments.Any(p => p.Status == PaymentStatus.Completed)
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
                IsPaymentVerified = o.PaidAmount > 0 && o.Payments.Any(p => p.Status == PaymentStatus.Completed),
                IsCancelled = o.Status == OrderStatus.Cancelled,
                HasSubmittedTrx = !string.IsNullOrEmpty(o.TransactionId),
                TransactionId = o.TransactionId
            }).FirstOrDefaultAsync();

            if (order is null) return Results.NotFound();
            return Results.Ok(order);
        });

        group.MapGet("/{ref}/sse", (string @ref, AppDbContext db, CancellationToken ct) =>
        {
            return TypedResults.ServerSentEvents(StreamOrderPaymentEvents(@ref, db, ct));
        });

        group.MapGet("/payment-sse", (HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var @ref = context.Request.Query["orderid"].ToString();
            if (string.IsNullOrWhiteSpace(@ref)) @ref = context.Request.Query["token"].ToString();
            return TypedResults.ServerSentEvents(StreamOrderPaymentEvents(@ref, db, ct));
        });

        return endpoints;
    }

    private static async IAsyncEnumerable<SseItem<OrderPaymentSseDto>> StreamOrderPaymentEvents(
        string? orderRef,
        AppDbContext db,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(orderRef)) yield break;

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
            yield break;
        }

        var verified = order.PaidAmount > 0 && order.Payments.Any(p => p.Status == PaymentStatus.Completed);
        var cancelled = order.Status == OrderStatus.Cancelled;

        // Yield initial state immediately
        yield return new SseItem<OrderPaymentSseDto>(
            new OrderPaymentSseDto(order.Id, order.PublicToken, verified, cancelled, order.Status.ToString()));

        if (verified || cancelled) yield break;

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
                    IsVerified = o.PaidAmount > 0 && o.Payments.Any(p => p.Status == PaymentStatus.Completed),
                    IsCancelled = o.Status == OrderStatus.Cancelled
                })
                .FirstOrDefaultAsync(ct);

            if (cur == null) yield break;

            if (cur.IsVerified || cur.IsCancelled)
            {
                yield return new SseItem<OrderPaymentSseDto>(
                    new OrderPaymentSseDto(cur.Id, cur.PublicToken, cur.IsVerified, cur.IsCancelled, cur.Status));
                yield break;
            }
        }
    }
}
