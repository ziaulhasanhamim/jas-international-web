using System.Data;
using System.Data.Common;
using JASIntWeb.Persistence;
using JASIntWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JASIntWeb.Components.Admin;

var checks = 0;
var now = new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero); // 13 October in Bangladesh.
SalesBucket Day(int year, int month, int day, decimal sales, decimal cost, int estimates = 0) =>
    new(new(year, month, day), new(1, 2, sales, cost, 80, estimates));
var days = new[]
{
    Day(2025, 10, 13, 20, 10),
    Day(2025, 10, 14, 30, 20),
    Day(2026, 9, 30, 1000, 950, 2),
    Day(2026, 10, 5, 70, 60),
    Day(2026, 10, 6, 80, 70),
    Day(2026, 10, 7, 90, 80),
    Day(2026, 10, 11, 100, 90),
    Day(2026, 10, 12, 120, 100),
    Day(2026, 10, 13, 200, 50)
};
var summary = SalesSummary.Create(days, now);
Check(summary.Select("today").Period.Start == new DateOnly(2026, 10, 13), "Bangladesh midnight boundary");
Check(summary.Select("today").Totals.Sales == 200 && summary.Select("today").Totals.Profit == 150, "today sales and gross profit");
Check(summary.Select("last-7-days").Totals.Orders == 4 && summary.Select("last-7-days").Totals.Sales == 510, "rolling seven days includes today and excludes day eight");
Check(summary.Select("last-week").Period.Start == new DateOnly(2026, 10, 5) && summary.Select("last-week").Period.End == new DateOnly(2026, 10, 11)
    && summary.Select("last-week").Totals.Orders == 4, "previous Monday–Sunday week");
Check(summary.Select("this-month").Totals.Sales == 660 && summary.Select("last-month").Totals.Sales == 1000, "calendar month boundaries");
Check(summary.Select("current-year").Totals.Orders == 7, "current year excludes previous year");
Check(summary.Select("last-365-days").Totals.Orders == 8, "365-day inclusive boundary");
Check(summary.Select("all-time").Totals.Orders == 9 && summary.Select("all-time").Totals.EstimatedUnits == 2, "all-time totals and estimate counts");
Check(summary.History.BestSalesDay?.Label == "30 Sep 2026" && summary.History.BestProfitDay?.Label == "13 Oct 2026", "sales and profit day winners differ");
Check(summary.History.BestSalesMonth?.Label == "September 2026" && summary.History.BestProfitMonth?.Label == "October 2026", "sales and profit month winners differ");
Check(summary.History.BestSalesYear?.Label == "2026" && summary.History.BestProfitYear?.Label == "2026", "all-time year records");
Check(summary.Select("today").Totals.Revenue == 280 && summary.Select("today").Totals.AverageOrder == 200
    && summary.Select("today").Totals.Margin == 75, "delivery fees excluded from profit and product metrics");
Check(summary.Select("invalid").Period.Key == "today", "unknown period defaults to today");
var empty = SalesSummary.Create([], now);
Check(empty.Periods.All(p => p.Totals == SalesTotals.Empty) && empty.History.BestSalesDay is null
    && empty.Select("today").Totals.Margin == 0, "empty history has safe zero metrics and no records");
var loss = SalesSummary.Create([Day(2026, 10, 13, 100, 150)], now);
Check(loss.Select("today").Totals.Profit == -50 && loss.Select("today").Totals.Margin == -50, "losses retain their sign");
var tied = SalesSummary.Create([Day(2026, 10, 13, 100, 50), Day(2026, 10, 12, 100, 50)], now);
Check(tied.History.BestProfitDay?.Label == "12 Oct 2026", "earliest date wins tied records");
var jan = SalesSummary.Create([], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(6)));
Check(jan.Select("last-month").Period.Start == new DateOnly(2025, 12, 1) && jan.Select("last-week").Period.Start == new DateOnly(2025, 12, 22), "month and week ranges cross years");
var leap = SalesSummary.Create([], new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.FromHours(6)));
Check(leap.Select("last-month").Period.End == new DateOnly(2024, 2, 29) && leap.Select("last-365-days").Period.End.DayNumber - leap.Select("last-365-days").Period.Start.DayNumber == 364, "leap years preserve calendar and rolling ranges");

// Ask the actual PostgreSQL provider to translate every production query. Supply
// empty readers after translation so these checks never need or modify a database.
var recorder = new EmptyQueryRecorder();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
    .AddInterceptors(new NoConnection(), recorder).Options);
var metrics = new SalesMetricsService(db);
await metrics.GetSummaryAsync(now);
foreach (var sort in new[] { "sales", "profit", "units", "name" })
{
    var page = await metrics.GetItemsAsync(summary.Select("today").Period, "phone", sort, int.MaxValue);
    Check(page.Page == 1 && page.Items.Count == 0, $"PostgreSQL query translation and safe paging: {sort}");
}
Check(recorder.Commands.Count == 10 && recorder.Commands.All(sql => sql.Contains("SELECT")), "all aggregate and item queries translate");
Check(recorder.Commands.Take(2).All(sql => sql.Contains("AT TIME ZONE 'UTC'")), "daily groups do not depend on database timezone");
var migrationSql = db.GetService<IMigrator>().GenerateScript("20261012180941_InitialCreate", "20261013062628_SalesMetrics");
Check(migrationSql.Contains("SET \"UnitCost\" = variant.\"CostPrice\", \"CostIsEstimated\" = TRUE")
    && migrationSql.Contains("SET \"DeliveredAt\" = COALESCE(\"UpdatedAt\", \"CreatedAt\")"), "migration generates frozen cost estimates and delivery-date backfill");

await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
await renderer.Dispatcher.InvokeAsync(async () =>
{
    var table = await renderer.RenderComponentAsync<SalesPeriodSummary>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Summary"] = summary }));
    var tableHtml = table.ToHtmlString();
    Check(tableHtml.Contains("last-365-days") && tableHtml.Contains("Includes estimated costs"), "period table renders ranges and cost estimate labels");
    var chart = await renderer.RenderComponentAsync<SalesTrend>(ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        ["Rows"] = new[] { Day(2026, 10, 12, 100, 150), Day(2026, 10, 13, 200, 50) },
        ["Period"] = summary.Select("last-7-days").Period
    }));
    Check(chart.ToHtmlString().Contains("polyline") && !chart.ToHtmlString().Contains("NaN"), "sales chart renders positive sales and negative profit");
});
Console.WriteLine($"Passed {checks} sales metrics checks.");

void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAILED: {name}");
    checks++;
    Console.WriteLine($"PASS: {name}");
}

sealed class NoConnection : DbConnectionInterceptor
{
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult.Suppress());
}

sealed class EmptyQueryRecorder : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);
        var table = new DataTable();
        // Empty result sets exercise SQL translation, never simulate stored sales.
        if (command.CommandText.StartsWith("SELECT count(*)::int"))
        {
            table.Columns.Add("Count", typeof(int));
            table.Rows.Add(0);
        }
        else if (command.CommandText.Contains("count(*)::int"))
        {
            table.Columns.Add("Date", typeof(DateTime));
            table.Columns.Add("Orders", typeof(int));
            table.Columns.Add("DeliveryFees", typeof(decimal));
        }
        else
        {
            for (var i = 0; i < 8; i++) table.Columns.Add($"Column{i}", typeof(object));
        }
        return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
    }
}
