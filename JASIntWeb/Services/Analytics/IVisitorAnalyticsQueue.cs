using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Services.Analytics;

public interface IVisitorAnalyticsQueue
{
    ValueTask EnqueueAsync(VisitorLog log, CancellationToken ct = default);
    IAsyncEnumerable<VisitorLog> ReadAllAsync(CancellationToken ct = default);
}
