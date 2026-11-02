namespace JASIntWeb.Services;

public sealed record SalesTotals(int Orders, int Units, decimal Sales, decimal Cost, decimal DeliveryFees, int EstimatedUnits)
{
    public static SalesTotals Empty { get; } = new(0, 0, 0, 0, 0, 0);
    public decimal Profit => Sales - Cost;
    public decimal Revenue => Sales + DeliveryFees;
    public decimal Margin => Sales == 0 ? 0 : Profit / Sales * 100;
    public decimal AverageOrder => Orders == 0 ? 0 : Sales / Orders;

    public static SalesTotals Sum(IEnumerable<SalesTotals> totals) => totals.Aggregate(Empty, (a, b) =>
        new(a.Orders + b.Orders, a.Units + b.Units, a.Sales + b.Sales, a.Cost + b.Cost,
            a.DeliveryFees + b.DeliveryFees, a.EstimatedUnits + b.EstimatedUnits));
}

public sealed record SalesBucket(DateOnly Date, SalesTotals Totals);

public sealed record SalesPeriod(string Key, string Label, DateOnly Start, DateOnly End)
{
    public DateTimeOffset StartUtc => MidnightUtc(Start);
    public DateTimeOffset EndUtc => MidnightUtc(End.AddDays(1));
    public string DateLabel => Start == End ? Start.ToString("dd MMM yyyy") : $"{Start:dd MMM yyyy} – {End:dd MMM yyyy}";
    private static DateTimeOffset MidnightUtc(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(6)).ToUniversalTime();
}

public sealed record PeriodSales(SalesPeriod Period, SalesTotals Totals);
public sealed record SalesRecord(string Label, SalesTotals Totals);
public sealed record SalesHistory(SalesRecord? BestSalesDay, SalesRecord? BestProfitDay,
    SalesRecord? BestSalesMonth, SalesRecord? BestProfitMonth, SalesRecord? BestSalesYear, SalesRecord? BestProfitYear);

public sealed record SalesSummary(IReadOnlyList<SalesBucket> Days, IReadOnlyList<PeriodSales> Periods, SalesHistory History)
{
    public PeriodSales Select(string? key) => Periods.FirstOrDefault(p => p.Period.Key == key) ?? Periods[0];

    public static SalesSummary Create(IReadOnlyList<SalesBucket> days, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(6)).DateTime);
        var month = new DateOnly(today.Year, today.Month, 1);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var periods = new SalesPeriod[]
        {
            new("today", "Today", today, today),
            new("last-7-days", "Last 7 days", today.AddDays(-6), today),
            new("last-week", "Last week", monday.AddDays(-7), monday.AddDays(-1)),
            new("this-month", "This month", month, today),
            new("last-month", "Last month", month.AddMonths(-1), month.AddDays(-1)),
            new("current-year", "Current year", new(today.Year, 1, 1), today),
            new("last-365-days", "Last 365 days", today.AddDays(-364), today),
            new("all-time", "All time", days.Count == 0 ? today : days.Min(d => d.Date), today)
        };
        var daily = days.OrderBy(d => d.Date).ToArray();
        var months = daily.GroupBy(d => new DateOnly(d.Date.Year, d.Date.Month, 1))
            .OrderBy(g => g.Key).Select(g => new SalesBucket(g.Key, SalesTotals.Sum(g.Select(d => d.Totals)))).ToArray();
        var years = daily.GroupBy(d => new DateOnly(d.Date.Year, 1, 1))
            .OrderBy(g => g.Key).Select(g => new SalesBucket(g.Key, SalesTotals.Sum(g.Select(d => d.Totals)))).ToArray();

        // Earliest period wins ties, so history records remain deterministic.
        SalesRecord? Best(IEnumerable<SalesBucket> buckets, bool profit, string format)
        {
            var winner = buckets.OrderByDescending(b => profit ? b.Totals.Profit : b.Totals.Sales).ThenBy(b => b.Date).FirstOrDefault();
            return winner is null ? null : new(winner.Date.ToString(format), winner.Totals);
        }

        return new(daily, periods.Select(p => new PeriodSales(p,
                SalesTotals.Sum(daily.Where(d => d.Date >= p.Start && d.Date <= p.End).Select(d => d.Totals)))).ToArray(),
            new(Best(daily, false, "dd MMM yyyy"), Best(daily, true, "dd MMM yyyy"),
                Best(months, false, "MMMM yyyy"), Best(months, true, "MMMM yyyy"),
                Best(years, false, "yyyy"), Best(years, true, "yyyy")));
    }
}

public sealed class SoldItemSales
{
    public int VariantId { get; init; }
    public string Product { get; init; } = "";
    public string Variant { get; init; } = "";
    public int Units { get; init; }
    public decimal Sales { get; init; }
    public decimal Cost { get; init; }
    public int EstimatedUnits { get; init; }
    public decimal Profit => Sales - Cost;
    public decimal Margin => Sales == 0 ? 0 : Profit / Sales * 100;
}

public sealed record SoldItemsPage(IReadOnlyList<SoldItemSales> Items, int Total, int Page)
{
    public const int PageSize = 50;
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
