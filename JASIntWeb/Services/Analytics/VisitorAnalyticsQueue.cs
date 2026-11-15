using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JASIntWeb.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace JASIntWeb.Services.Analytics;

public sealed class VisitorAnalyticsQueue : IVisitorAnalyticsQueue
{
    private readonly Channel<VisitorLog> _channel;
    private readonly ILogger<VisitorAnalyticsQueue> _logger;

    public VisitorAnalyticsQueue(ILogger<VisitorAnalyticsQueue> logger)
    {
        _logger = logger;
        // Bounded channel with DropOldest to protect memory during massive ad traffic surges
        _channel = Channel.CreateBounded<VisitorLog>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(VisitorLog log, CancellationToken ct = default)
    {
        if (!_channel.Writer.TryWrite(log))
        {
            _logger.LogWarning("Analytics queue is full; dropped oldest event.");
        }
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<VisitorLog> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken ct = default)
    {
        return _channel.Reader.WaitToReadAsync(ct);
    }

    public bool TryRead([NotNullWhen(true)] out VisitorLog? log)
    {
        return _channel.Reader.TryRead(out log);
    }
}
