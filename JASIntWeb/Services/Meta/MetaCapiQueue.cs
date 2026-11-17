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
    public const int Capacity = 10_000;

    private readonly Channel<MetaCapiEventPayload> _channel = Channel.CreateBounded<MetaCapiEventPayload>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(MetaCapiEventPayload payload) => TryEnqueue(payload)
        ? ValueTask.CompletedTask
        : ValueTask.FromException(new InvalidOperationException("The Meta conversion queue is full."));

    public bool TryEnqueue(MetaCapiEventPayload payload) => _channel.Writer.TryWrite(payload);

    public IAsyncEnumerable<MetaCapiEventPayload> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public int PendingCount => _channel.Reader.Count;
}
