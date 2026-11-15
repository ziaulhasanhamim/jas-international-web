using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JASIntWeb.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JASIntWeb.Services.Analytics;

public sealed class VisitorAnalyticsService : IVisitorAnalyticsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<VisitorAnalyticsService> _logger;

    public VisitorAnalyticsService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<VisitorAnalyticsService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<int> GetLiveVisitorCountAsync(TimeSpan? window = null, CancellationToken ct = default)
    {
        try
        {
            var span = window ?? TimeSpan.FromMinutes(5);
            var cutoff = DateTimeOffset.UtcNow.Subtract(span);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await db.VisitorLogs
                .AsNoTracking()
                .Where(x => x.Timestamp >= cutoff)
                .Select(x => x.VisitorId)
                .Distinct()
                .CountAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get live visitor count from database.");
            return 0;
        }
    }

    public async Task<VisitorDashboardData> GetDashboardDataAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var query = db.VisitorLogs
                .AsNoTracking()
                .Where(x => x.Timestamp >= start && x.Timestamp <= end);

            var totalPageviews = await query.CountAsync(ct);

            if (totalPageviews == 0)
            {
                var live = await GetLiveVisitorCountAsync(TimeSpan.FromMinutes(5), ct);
                return new VisitorDashboardData(
                    new VisitorAnalyticsSummary(0, 0, 0, 0.0, 0.0, live),
                    [], [], [], [], [], [], [], []
                );
            }

            var uniqueVisitors = await query
                .Select(x => x.VisitorId)
                .Distinct()
                .CountAsync(ct);

            var sessionCounts = await query
                .GroupBy(x => x.SessionId)
                .Select(g => g.Count())
                .ToListAsync(ct);

            var totalSessions = sessionCounts.Count;
            var singlePageSessions = sessionCounts.Count(c => c == 1);

            var bounceRate = totalSessions > 0
                ? Math.Round((double)singlePageSessions / totalSessions * 100.0, 1)
                : 0.0;

            var avgPages = totalSessions > 0
                ? Math.Round((double)totalPageviews / totalSessions, 1)
                : 0.0;

            var liveVisitors = await GetLiveVisitorCountAsync(TimeSpan.FromMinutes(5), ct);

            var summary = new VisitorAnalyticsSummary(
                totalPageviews,
                uniqueVisitors,
                totalSessions,
                bounceRate,
                avgPages,
                liveVisitors
            );

            // Top Pages
            var topPagesRaw = await query
                .GroupBy(x => x.Path)
                .Select(g => new
                {
                    Path = g.Key,
                    Title = g.Max(x => x.Title),
                    Pageviews = g.Count()
                })
                .OrderByDescending(x => x.Pageviews)
                .Take(15)
                .ToListAsync(ct);

            var topPages = topPagesRaw
                .Select(p => new PageMetric(p.Path, p.Title, p.Pageviews, 0))
                .ToList();

            // Top Sources / Referrers
            var topSourcesRaw = await query
                .GroupBy(x => x.ReferrerDomain)
                .Select(g => new
                {
                    Source = g.Key,
                    Pageviews = g.Count()
                })
                .OrderByDescending(x => x.Pageviews)
                .Take(10)
                .ToListAsync(ct);

            var topSources = topSourcesRaw
                .Select(s => new TrafficSourceMetric(
                    string.IsNullOrWhiteSpace(s.Source) ? "Direct" : s.Source,
                    0,
                    s.Pageviews,
                    totalPageviews > 0 ? Math.Round((double)s.Pageviews / totalPageviews * 100.0, 1) : 0.0))
                .ToList();

            // Devices
            var devicesRaw = await query
                .GroupBy(x => x.DeviceType)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(ct);

            var devices = devicesRaw
                .Select(d => new DeviceMetric(
                    string.IsNullOrWhiteSpace(d.Name) ? "Desktop" : d.Name,
                    d.Count,
                    Math.Round((double)d.Count / totalPageviews * 100.0, 1)))
                .ToList();

            // Browsers
            var browsersRaw = await query
                .GroupBy(x => x.Browser)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToListAsync(ct);

            var browsers = browsersRaw
                .Select(b => new DeviceMetric(
                    string.IsNullOrWhiteSpace(b.Name) ? "Other" : b.Name,
                    b.Count,
                    Math.Round((double)b.Count / totalPageviews * 100.0, 1)))
                .ToList();

            // Operating Systems
            var osRaw = await query
                .GroupBy(x => x.OperatingSystem)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToListAsync(ct);

            var operatingSystems = osRaw
                .Select(o => new DeviceMetric(
                    string.IsNullOrWhiteSpace(o.Name) ? "Other" : o.Name,
                    o.Count,
                    Math.Round((double)o.Count / totalPageviews * 100.0, 1)))
                .ToList();

            // Top Locations (City/Country)
            var locationsRaw = await query
                .Where(x => x.City != null || x.Country != null)
                .GroupBy(x => new { x.City, x.Country })
                .Select(g => new { g.Key.City, g.Key.Country, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(ct);

            var topLocations = locationsRaw
                .Select(l => new LocationMetric(
                    string.IsNullOrEmpty(l.Country) ? (l.City ?? "Unknown") : $"{l.City ?? "Unknown"}, {l.Country}",
                    l.Count,
                    Math.Round((double)l.Count / totalPageviews * 100.0, 1)))
                .ToList();

            // UTM Campaigns
            var campaignsRaw = await query
                .Where(x => x.UtmCampaign != null)
                .GroupBy(x => new { x.UtmCampaign, x.UtmSource, x.UtmMedium })
                .Select(g => new
                {
                    Campaign = g.Key.UtmCampaign,
                    Source = g.Key.UtmSource,
                    Medium = g.Key.UtmMedium,
                    Pageviews = g.Count()
                })
                .OrderByDescending(x => x.Pageviews)
                .Take(10)
                .ToListAsync(ct);

            var topCampaigns = campaignsRaw
                .Select(c => new CampaignMetric(c.Campaign ?? "General", c.Source ?? "", c.Medium ?? "", 0, c.Pageviews))
                .ToList();

            // Recent Activity
            var recent = await GetRecentActivityAsync(30, ct);

            return new VisitorDashboardData(
                summary,
                topPages,
                topSources,
                devices,
                browsers,
                operatingSystems,
                topLocations,
                topCampaigns,
                recent
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load visitor analytics dashboard data.");
            var live = await GetLiveVisitorCountAsync(TimeSpan.FromMinutes(5), ct);
            return new VisitorDashboardData(
                new VisitorAnalyticsSummary(0, 0, 0, 0.0, 0.0, live),
                [], [], [], [], [], [], [], []
            );
        }
    }

    public async Task<List<RecentActivityItem>> GetRecentActivityAsync(int limit = 50, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var items = await db.VisitorLogs
                .AsNoTracking()
                .OrderByDescending(x => x.Timestamp)
                .Take(limit)
                .Select(x => new RecentActivityItem(
                    x.Id,
                    x.Timestamp,
                    x.Path,
                    x.Title,
                    x.DeviceType,
                    x.Browser,
                    x.OperatingSystem,
                    x.City,
                    x.Country,
                    x.ReferrerDomain ?? "Direct",
                    x.UtmCampaign,
                    x.IpAddress,
                    x.DurationSeconds
                ))
                .ToListAsync(ct);

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get recent visitor activity from database.");
            return [];
        }
    }
}
