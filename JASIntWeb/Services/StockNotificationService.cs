using System.Net.Mail;
using JASIntWeb.Helpers;

namespace JASIntWeb.Services;

public interface IStockNotificationMailTransport
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(string email, string subject, string message, CancellationToken ct);
}

public sealed class StockNotificationMailTransport(AccountMailService mail) : IStockNotificationMailTransport
{
    public bool IsConfigured => mail.IsConfigured;
    public Task<bool> SendAsync(string email, string subject, string message, CancellationToken ct) => mail.SendAsync(email, subject, message);
}

public sealed class StockNotificationService(AppDbContext db, IStockNotificationMailTransport mail, IConfiguration configuration)
{
    public bool MailConfigured => mail.IsConfigured;

    public async Task<Result> TrySubscribeAsync(int variantId, string email, CancellationToken ct = default)
    {
        email = email.Trim();
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            return Error.Validation("Enter a valid email address.");
        if (!mail.IsConfigured) return Error.Failure("Email notifications are temporarily unavailable. Please contact the store.", 503);
        var normalized = email.ToUpperInvariant();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({$"stock:{variantId}:{normalized}"}, 0))", ct);
        if (await db.StockNotifications.AnyAsync(x => x.ProductVariantId == variantId && x.NormalizedEmail == normalized
            && (x.Status == StockNotificationStatus.Pending || x.Status == StockNotificationStatus.Active || x.Status == StockNotificationStatus.Failed), ct))
            return Result.Success;
        var variant = await db.ProductVariants.AsNoTracking().Where(v => v.Id == variantId && v.IsActive && !v.IsDeleted
            && !v.Product!.IsDeleted && !v.Product.Category!.IsDeleted && (v.Product.Category.ParentCategory == null || !v.Product.Category.ParentCategory.IsDeleted)).SingleOrDefaultAsync(ct);
        if (variant == null) return Error.NotFound("This product option is unavailable.");
        if (await AvailableAsync(variantId, ct)) return Error.Conflict("This option is in stock. You can add it to your cart.");
        db.StockNotifications.Add(new StockNotification { ProductVariantId = variantId, Email = email, NormalizedEmail = normalized, Status = StockNotificationStatus.Pending });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Success;
    }

    public Task<Result> SubscribeAsync(int variantId, string email, CancellationToken ct = default)
        => TrySubscribeAsync(variantId, email, ct);

    public async Task<bool> ChangePreferenceAsync(Guid token, bool confirm, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.StockNotifications.Where(x => x.Token == token);
        if (confirm)
            return await query.Where(x => x.Status == StockNotificationStatus.Pending).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, StockNotificationStatus.Active).SetProperty(x => x.ConfirmedAt, now)
                .SetProperty(x => x.Attempts, 0).SetProperty(x => x.NextAttemptAt, now).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseToken, (Guid?)null), ct) > 0;
        return await query.Where(x => x.Status != StockNotificationStatus.Sent).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, StockNotificationStatus.Unsubscribed).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
            .SetProperty(x => x.LeaseToken, (Guid?)null), ct) > 0;
    }

    private Task<bool> AvailableAsync(int variantId, CancellationToken ct) => db.ProductVariants.AsNoTracking().AnyAsync(v => v.Id == variantId
        && v.IsActive && !v.IsDeleted && !v.Product!.IsDeleted && !v.Product.Category!.IsDeleted
        && (v.Product.Category.ParentCategory == null || !v.Product.Category.ParentCategory.IsDeleted)
        && db.InventoryItems.Any(i => i.ProductVariantId == v.Id && i.QuantityOnHand > i.QuantityReserved), ct);

    // Persisted lease survives worker restarts. SMTP delivery is at least once: a crash after
    // sending but before recording success may duplicate an email; never silently discard it.
    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        if (!mail.IsConfigured) return false;
        var now = DateTimeOffset.UtcNow;
        StockNotification? notification;
        var lease = Guid.NewGuid();
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            notification = await db.StockNotifications.FromSql($"""
                SELECT * FROM "StockNotifications" n
                WHERE n."NextAttemptAt" <= {now} AND (n."LeaseUntil" IS NULL OR n."LeaseUntil" < {now})
                AND ((n."Status" = 0 AND n."ConfirmationSentAt" IS NULL)
                  OR (n."Status" = 1 AND EXISTS (
                    SELECT 1 FROM "InventoryItems" i JOIN "ProductVariants" v ON v."Id" = i."ProductVariantId"
                    JOIN "Products" p ON p."Id" = v."ProductId" JOIN "Categories" c ON c."Id" = p."CategoryId"
                    LEFT JOIN "Categories" parent ON parent."Id" = c."ParentCategoryId"
                    WHERE i."ProductVariantId" = n."ProductVariantId" AND i."QuantityOnHand" > i."QuantityReserved"
                      AND v."IsActive" AND NOT v."IsDeleted" AND NOT p."IsDeleted" AND NOT c."IsDeleted"
                      AND (parent."Id" IS NULL OR NOT parent."IsDeleted"))))
                ORDER BY n."NextAttemptAt", n."Id" LIMIT 1 FOR UPDATE OF n SKIP LOCKED
                """).AsTracking().FirstOrDefaultAsync(ct);
            if (notification == null) return false;
            notification.LeaseUntil = now.AddMinutes(5);
            notification.LeaseToken = lease;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        var originalStatus = notification.Status;
        var baseUrl = configuration["PublicBaseUrl"]?.TrimEnd('/') ?? "https://www.jasinternational.com.bd";
        var preferenceUrl = $"{baseUrl}/stock-notifications/{notification.Token}";
        bool sent;
        try
        {
            if (originalStatus == StockNotificationStatus.Pending)
                sent = await mail.SendAsync(notification.Email, "Confirm your stock alert · JAS International",
                    $"Confirm your request: {preferenceUrl}/confirm\nCancel this request: {preferenceUrl}/unsubscribe\nIf you did not request this email, you can ignore it.", ct);
            else
            {
                var product = await db.ProductVariants.AsNoTracking().Where(v => v.Id == notification.ProductVariantId)
                    .Select(v => new { v.Product!.Name, v.Product.Slug, v.SKU }).SingleAsync(ct);
                sent = await mail.SendAsync(notification.Email, $"Back in stock: {product.Name}",
                    $"{product.Name} ({product.SKU}) is available again.\n{baseUrl}/product/{Uri.EscapeDataString(product.Slug)}?variant={notification.ProductVariantId}\nStock is subject to availability. This alert does not reserve stock.\nManage your request: {preferenceUrl}/unsubscribe", ct);
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested) { sent = false; }
        // Conditional update prevents a late send result from reactivating an unsubscribed request.
        var owned = db.StockNotifications.Where(x => x.Id == notification.Id && x.LeaseToken == lease && x.Status == originalStatus);
        if (sent)
        {
            await owned.ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null).SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LastError, (string?)null).SetProperty(x => x.Attempts, 0)
                .SetProperty(x => x.ConfirmationSentAt, originalStatus == StockNotificationStatus.Pending ? now : notification.ConfirmationSentAt)
                .SetProperty(x => x.NotificationSentAt, originalStatus == StockNotificationStatus.Active ? now : notification.NotificationSentAt)
                .SetProperty(x => x.Status, originalStatus == StockNotificationStatus.Active ? StockNotificationStatus.Sent : originalStatus), ct);
        }
        else
        {
            var attempts = notification.Attempts + 1;
            await owned.ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null).SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.Attempts, attempts).SetProperty(x => x.NextAttemptAt, now.AddMinutes(Math.Min(60, Math.Pow(2, attempts))))
                .SetProperty(x => x.LastError, "Email delivery failed. Check the mail provider and retry.")
                .SetProperty(x => x.Status, attempts >= 5 ? StockNotificationStatus.Failed : originalStatus), ct);
        }
        db.Entry(notification).State = EntityState.Detached;
        return true;
    }
}

public sealed class StockNotificationWorker(IServiceScopeFactory scopes, ILogger<StockNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<StockNotificationService>();
                for (var count = 0; count < 25 && await service.ProcessNextAsync(stoppingToken); count++) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Stock notification processing failed; retrying next cycle."); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
