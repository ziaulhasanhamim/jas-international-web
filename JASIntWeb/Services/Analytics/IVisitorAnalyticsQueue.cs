using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Services.Analytics;

public interface IVisitorAnalyticsQueue
{
    ValueTask EnqueueAsync(VisitorLog log, CancellationToken ct = default);
    IAsyncEnumerable<VisitorLog> ReadAllAsync(CancellationToken ct = default);
    ValueTask<bool> WaitToReadAsync(CancellationToken ct = default);
    bool TryRead([NotNullWhen(true)] out VisitorLog? log);
}
