using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Asynchronously wait until items are available in the channel
                if (await _queue.WaitToReadAsync(stoppingToken))
                {
                    // Drain up to 50 items immediately without thread blocking
                    while (batch.Count < 50 && _queue.TryRead(out var item))
                    {
                        if (item is not null)
                        {
                            batch.Add(item);
                        }
                    }

                    if (batch.Count > 0)
                    {
                        await FlushBatchAsync(batch, stoppingToken);
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
