using System.Threading.Channels;
using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Services.Analytics;

public sealed class VisitorAnalyticsQueue : IVisitorAnalyticsQueue
{
    private readonly Channel<VisitorLog> _channel;
    private readonly ILogger<VisitorAnalyticsQueue> _logger;

    public VisitorAnalyticsQueue(ILogger<VisitorAnalyticsQueue> logger)
    {
        _logger = logger;
        // Bounded channel to prevent unbounded memory growth during extreme traffic bursts
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
}
