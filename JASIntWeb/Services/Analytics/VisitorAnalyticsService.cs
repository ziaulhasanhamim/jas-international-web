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

    public async Task<VisitorDashboardData> GetDashboardDataAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
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

        var totalSessions = await query
            .Select(x => x.SessionId)
            .Distinct()
            .CountAsync(ct);

        var singlePageSessions = await query
            .GroupBy(x => x.SessionId)
            .Where(g => g.Count() == 1)
            .CountAsync(ct);

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
            .GroupBy(x => string.IsNullOrEmpty(x.ReferrerDomain) ? "Direct" : x.ReferrerDomain)
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
                s.Source,
                0,
                s.Pageviews,
                totalPageviews > 0 ? Math.Round((double)s.Pageviews / totalPageviews * 100.0, 1) : 0.0))
            .ToList();

        // Devices
        var devicesRaw = await query
            .GroupBy(x => string.IsNullOrEmpty(x.DeviceType) ? "Desktop" : x.DeviceType)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var devices = devicesRaw
            .Select(d => new DeviceMetric(d.Name, d.Count, Math.Round((double)d.Count / totalPageviews * 100.0, 1)))
            .ToList();

        // Browsers
        var browsersRaw = await query
            .GroupBy(x => string.IsNullOrEmpty(x.Browser) ? "Other" : x.Browser)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToListAsync(ct);

        var browsers = browsersRaw
            .Select(b => new DeviceMetric(b.Name, b.Count, Math.Round((double)b.Count / totalPageviews * 100.0, 1)))
            .ToList();

        // Operating Systems
        var osRaw = await query
            .GroupBy(x => string.IsNullOrEmpty(x.OperatingSystem) ? "Other" : x.OperatingSystem)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToListAsync(ct);

        var operatingSystems = osRaw
            .Select(o => new DeviceMetric(o.Name, o.Count, Math.Round((double)o.Count / totalPageviews * 100.0, 1)))
            .ToList();

        // Top Locations (City/Country)
        var locationsRaw = await query
            .Where(x => !string.IsNullOrEmpty(x.City) || !string.IsNullOrEmpty(x.Country))
            .GroupBy(x => (x.City ?? "Unknown") + (string.IsNullOrEmpty(x.Country) ? "" : $", {x.Country}"))
            .Select(g => new { Location = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        var topLocations = locationsRaw
            .Select(l => new LocationMetric(l.Location, l.Count, Math.Round((double)l.Count / totalPageviews * 100.0, 1)))
            .ToList();

        // UTM Campaigns
        var campaignsRaw = await query
            .Where(x => !string.IsNullOrEmpty(x.UtmCampaign))
            .GroupBy(x => new { x.UtmCampaign, x.UtmSource, x.UtmMedium })
            .Select(g => new
            {
                Campaign = g.Key.UtmCampaign!,
                Source = g.Key.UtmSource ?? "",
                Medium = g.Key.UtmMedium ?? "",
                Pageviews = g.Count()
            })
            .OrderByDescending(x => x.Pageviews)
            .Take(10)
            .ToListAsync(ct);

        var topCampaigns = campaignsRaw
            .Select(c => new CampaignMetric(c.Campaign, c.Source, c.Medium, 0, c.Pageviews))
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

    public async Task<List<RecentActivityItem>> GetRecentActivityAsync(int limit = 50, CancellationToken ct = default)
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
}
