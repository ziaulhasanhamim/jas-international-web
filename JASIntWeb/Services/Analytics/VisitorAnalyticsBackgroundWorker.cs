using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace JASIntWeb.Services.Analytics;

public sealed class VisitorAnalyticsBackgroundWorker : BackgroundService
{
    private readonly IVisitorAnalyticsQueue _queue;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<VisitorAnalyticsBackgroundWorker> _logger;

    public VisitorAnalyticsBackgroundWorker(
        IVisitorAnalyticsQueue queue,
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<VisitorAnalyticsBackgroundWorker> logger)
    {
        _queue = queue;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Visitor Analytics background ingestion worker started.");
        var batch = new List<VisitorLog>(50);
        var flushInterval = TimeSpan.FromSeconds(2);
        var lastFlush = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Read from channel with cancellation support
                await foreach (var log in _queue.ReadAllAsync(stoppingToken))
                {
                    batch.Add(log);

                    if (batch.Count >= 50 || DateTimeOffset.UtcNow - lastFlush >= flushInterval)
                    {
                        await FlushBatchAsync(batch, stoppingToken);
                        lastFlush = DateTimeOffset.UtcNow;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Visitor Analytics background ingestion worker.");
                await Task.Delay(1000, stoppingToken);
            }
        }

        if (batch.Count > 0)
        {
            await FlushBatchAsync(batch, CancellationToken.None);
        }

        _logger.LogInformation("Visitor Analytics background ingestion worker stopped.");
    }

    private async Task FlushBatchAsync(List<VisitorLog> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        var itemsToSave = batch.ToList();
        batch.Clear();

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            await db.VisitorLogs.AddRangeAsync(itemsToSave, ct);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("Flushed {Count} visitor analytics events to database.", itemsToSave.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist batch of {Count} visitor logs to database.", itemsToSave.Count);
        }
    }
}
