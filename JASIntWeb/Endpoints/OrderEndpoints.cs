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

        return endpoints;
    }
}
