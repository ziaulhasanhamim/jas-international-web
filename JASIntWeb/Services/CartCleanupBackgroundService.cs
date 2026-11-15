using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JASIntWeb.Services;

public sealed class CartCleanupBackgroundService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<CartCleanupBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public CartCleanupBackgroundService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<CartCleanupBackgroundService> logger,
        TimeSpan? checkInterval = null)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromHours(12);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CartCleanupBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStaleCartsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during cart cleanup.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> CleanupStaleCartsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);

        // Delete carts that are:
        // 1. Converted to order (CartStatus.Converted)
        // 2. Inactive for more than 30 days (LastActivityAt < cutoff, or CreatedAt < cutoff)
        var staleCartIds = await db.Carts
            .Where(c => c.Status == CartStatus.Converted
                     || (c.LastActivityAt != null && c.LastActivityAt < cutoff)
                     || (c.LastActivityAt == null && c.CreatedAt < cutoff))
            .Select(c => c.Id)
            .Take(1000)
            .ToListAsync(ct);

        if (staleCartIds.Count == 0)
        {
            return 0;
        }

        // Delete associated cart items first
        await db.CartItems
            .Where(i => staleCartIds.Contains(i.CartId))
            .ExecuteDeleteAsync(ct);

        // Delete the cart records
        var deletedCount = await db.Carts
            .Where(c => staleCartIds.Contains(c.Id))
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("CartCleanup: Cleaned up {Count} converted or inactive carts older than 30 days.", deletedCount);
        return deletedCount;
    }
}
