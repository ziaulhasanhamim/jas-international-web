using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JASIntWeb.Services.Analytics;

public sealed record VisitorAnalyticsSummary(
    int TotalPageviews,
    int UniqueVisitors,
    int TotalSessions,
    double BounceRate,
    double AvgPagesPerSession,
    int LiveVisitorsCount
);

public sealed record PageMetric(string Path, string? Title, int Pageviews, int UniqueVisitors);

public sealed record TrafficSourceMetric(string Source, int Visitors, int Pageviews, double Percentage);

public sealed record DeviceMetric(string Name, int Count, double Percentage);

public sealed record LocationMetric(string Location, int Visitors, double Percentage);

public sealed record CampaignMetric(string Campaign, string Source, string Medium, int Visitors, int Pageviews);

public sealed record RecentActivityItem(
    long Id,
    DateTimeOffset Timestamp,
    string Path,
    string? Title,
    string DeviceType,
    string Browser,
    string OperatingSystem,
    string? City,
    string? Country,
    string ReferrerDomain,
    string? UtmCampaign,
    string? IpAddress,
    int DurationSeconds
);

public sealed record VisitorDashboardData(
    VisitorAnalyticsSummary Summary,
    List<PageMetric> TopPages,
    List<TrafficSourceMetric> TopSources,
    List<DeviceMetric> Devices,
    List<DeviceMetric> Browsers,
    List<DeviceMetric> OperatingSystems,
    List<LocationMetric> TopLocations,
    List<CampaignMetric> TopCampaigns,
    List<RecentActivityItem> RecentActivity
);

public interface IVisitorAnalyticsService
{
    Task<int> GetLiveVisitorCountAsync(TimeSpan? window = null, CancellationToken ct = default);
    Task<VisitorDashboardData> GetDashboardDataAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<List<RecentActivityItem>> GetRecentActivityAsync(int limit = 50, CancellationToken ct = default);
}
