using System.Threading.Channels;

namespace JASIntWeb.Services.Meta;

public sealed class MetaCapiEventPayload
{
    public required string EventName { get; init; }
    public required string EventId { get; init; }
    public string? EventSourceUrl { get; init; }
    public long EventTime { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public required MetaUserData UserData { get; init; }
    public MetaCustomData? CustomData { get; init; }
    public int RetryCount { get; set; } = 0;
}

public interface IMetaCapiQueue
{
    ValueTask EnqueueAsync(MetaCapiEventPayload payload);
    bool TryEnqueue(MetaCapiEventPayload payload);
    IAsyncEnumerable<MetaCapiEventPayload> ReadAllAsync(CancellationToken cancellationToken);
    int PendingCount { get; }
}

public sealed class MetaCapiQueue : IMetaCapiQueue
{
    private readonly Channel<MetaCapiEventPayload> _channel = Channel.CreateUnbounded<MetaCapiEventPayload>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(MetaCapiEventPayload payload) => _channel.Writer.WriteAsync(payload);

    public bool TryEnqueue(MetaCapiEventPayload payload) => _channel.Writer.TryWrite(payload);

    public IAsyncEnumerable<MetaCapiEventPayload> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public int PendingCount => _channel.Reader.Count;
}
