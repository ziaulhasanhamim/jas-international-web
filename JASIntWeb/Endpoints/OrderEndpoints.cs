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
                    o.DeliveryAddress != null ? o.DeliveryAddress.CustomerName : "Customer"
                ))
                .ToListAsync();

            return Results.Ok(orders);
        });

        group.MapGet("/{token:guid}/status", async (Guid token, AppDbContext db) =>
        {
            var order = await db.Orders.AsNoTracking()
                .Include(o => o.Payments)
                .Where(o => o.PublicToken == token)
                .Select(o => new
                {
                    o.Id,
                    o.PublicToken,
                    Status = o.Status.ToString(),
                    PaidAmount = o.PaidAmount,
                    IsPaymentVerified = o.PaidAmount > 0 && o.Payments.Any(p => p.Status == Persistence.Entities.PaymentStatus.Completed),
                    HasSubmittedTrx = !string.IsNullOrEmpty(o.TransactionId),
                    TransactionId = o.TransactionId
                })
                .FirstOrDefaultAsync();

            if (order is null) return Results.NotFound();
            return Results.Ok(order);
        });

        return endpoints;
    }
}
