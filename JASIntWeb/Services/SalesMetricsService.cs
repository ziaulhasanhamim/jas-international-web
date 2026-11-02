namespace JASIntWeb.Services;

// Admin SSR pages authorize access before querying these reports. Aggregate in PostgreSQL,
// returning daily totals and a page of sold items rather than loading all historical orders.
public sealed class SalesMetricsService(AppDbContext db)
{
    public async Task<SalesSummary> GetSummaryAsync(DateTimeOffset? at = null)
    {
        var now = (at ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var orders = db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Delivered
            && (o.DeliveredAt ?? o.UpdatedAt ?? o.CreatedAt) <= now);
        // Npgsql translates DateTimeOffset.DateTime to AT TIME ZONE 'UTC', then
        // shifts to Bangladesh before truncation. This does not depend on the DB session timezone.
        var orderDays = await orders.GroupBy(o => (o.DeliveredAt ?? o.UpdatedAt ?? o.CreatedAt).DateTime.AddHours(6).Date)
            .Select(g => new { Date = g.Key, Orders = g.Count(), DeliveryFees = g.Sum(o => o.DeliveryCharge) })
            .ToListAsync();
        var itemDays = await db.OrderItems.AsNoTracking().Where(i => i.Order!.Status == OrderStatus.Delivered
                && (i.Order.DeliveredAt ?? i.Order.UpdatedAt ?? i.Order.CreatedAt) <= now)
            .GroupBy(i => (i.Order!.DeliveredAt ?? i.Order.UpdatedAt ?? i.Order.CreatedAt).DateTime.AddHours(6).Date)
            .Select(g => new
            {
                Date = g.Key,
                Units = g.Sum(i => i.Quantity),
                Sales = g.Sum(i => i.LineTotal),
                Cost = g.Sum(i => i.UnitCost * i.Quantity),
                EstimatedUnits = g.Sum(i => i.CostIsEstimated ? i.Quantity : 0)
            }).ToDictionaryAsync(g => g.Date);
        var days = orderDays.Select(o =>
        {
            var items = itemDays.GetValueOrDefault(o.Date);
            return new SalesBucket(DateOnly.FromDateTime(o.Date),
                new(o.Orders, items?.Units ?? 0, items?.Sales ?? 0, items?.Cost ?? 0, o.DeliveryFees, items?.EstimatedUnits ?? 0));
        }).ToArray();
        return SalesSummary.Create(days, now);
    }

    public async Task<SoldItemsPage> GetItemsAsync(SalesPeriod period, string? search, string? sort, int page)
    {
        var now = DateTimeOffset.UtcNow;
        var lines = db.OrderItems.AsNoTracking().Where(i => i.Order!.Status == OrderStatus.Delivered
            && (i.Order.DeliveredAt ?? i.Order.UpdatedAt ?? i.Order.CreatedAt) >= period.StartUtc
            && (i.Order.DeliveredAt ?? i.Order.UpdatedAt ?? i.Order.CreatedAt) < period.EndUtc
            && (i.Order.DeliveredAt ?? i.Order.UpdatedAt ?? i.Order.CreatedAt) <= now);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            lines = lines.Where(i => EF.Functions.ILike(i.ProductName, term, "\\")
                || EF.Functions.ILike(i.VariantName, term, "\\")
                || EF.Functions.ILike(i.ProductVariant!.SKU, term, "\\"));
        }
        // Keep the product and variant descriptions recorded on each sale, including hidden products.
        var items = lines.GroupBy(i => new { i.ProductVariantId, i.ProductName, i.VariantName })
            .Select(g => new SoldItemSales
            {
                VariantId = g.Key.ProductVariantId,
                Product = g.Key.ProductName,
                Variant = g.Key.VariantName,
                Units = g.Sum(i => i.Quantity),
                Sales = g.Sum(i => i.LineTotal),
                Cost = g.Sum(i => i.UnitCost * i.Quantity),
                EstimatedUnits = g.Sum(i => i.CostIsEstimated ? i.Quantity : 0)
            });
        var total = await items.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)SoldItemsPage.PageSize)));
        var ordered = sort switch
        {
            "profit" => items.OrderByDescending(i => i.Sales - i.Cost),
            "units" => items.OrderByDescending(i => i.Units),
            "name" => items.OrderBy(i => i.Product),
            _ => items.OrderByDescending(i => i.Sales)
        };
        return new(await ordered.ThenBy(i => i.Product).ThenBy(i => i.Variant).ThenBy(i => i.VariantId)
            .Skip((page - 1) * SoldItemsPage.PageSize).Take(SoldItemsPage.PageSize).ToListAsync(), total, page);
    }
}
