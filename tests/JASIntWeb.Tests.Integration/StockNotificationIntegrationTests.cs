using JASIntWeb.Persistence;
using JASIntWeb.Services;
using JASIntWeb.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JASIntWeb.Tests.Integration;

public class StockNotificationIntegrationTests : JasIntegrationTestBase
{
    private sealed class FakeMail : IStockNotificationMailTransport
    {
        public bool IsConfigured { get; set; } = true;
        public bool Succeeds { get; set; } = true;
        public List<string> Subjects { get; } = [];
        public TaskCompletionSource? Entered { get; set; }
        public TaskCompletionSource? Release { get; set; }
        public async Task<bool> SendAsync(string email, string subject, string message, CancellationToken ct)
        {
            Subjects.Add(subject); Entered?.TrySetResult();
            if (Release != null) await Release.Task.WaitAsync(ct);
            return Succeeds;
        }
    }
    private static StockNotificationService Service(AppDbContext db, FakeMail mail) => new(db, mail,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["PublicBaseUrl"] = "https://example.test" }).Build());
    private async Task<Guid> SubscribeAsync(FakeMail mail, string email = "shopper@example.test")
    {
        await using var scope = CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.InventoryItems.Where(i => i.ProductVariantId == 1).ExecuteUpdateAsync(s => s.SetProperty(i => i.QuantityOnHand, 0));
        await Service(db, mail).SubscribeAsync(1, email);
        return await db.StockNotifications.Where(x => x.NormalizedEmail == email.ToUpperInvariant()).Select(x => x.Token).SingleAsync();
    }
    private async Task<bool> ProcessAsync(FakeMail mail)
    {
        await using var scope = CreateScope();
        return await Service(scope.ServiceProvider.GetRequiredService<AppDbContext>(), mail).ProcessNextAsync();
    }
    private async Task<StockNotification> ReadAsync(Guid token)
    {
        await using var scope = CreateScope(); return await scope.ServiceProvider.GetRequiredService<AppDbContext>().StockNotifications.AsNoTracking().SingleAsync(x => x.Token == token);
    }
    private async Task RestockAsync()
    {
        await using var scope = CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryItems.Where(i => i.ProductVariantId == 1).ExecuteUpdateAsync(s => s.SetProperty(i => i.QuantityOnHand, 10));
    }
    private async Task ConfirmAsync(Guid token)
    {
        await using var scope = CreateScope(); await Service(scope.ServiceProvider.GetRequiredService<AppDbContext>(), new FakeMail()).ChangePreferenceAsync(token, true);
    }

    [Test]
    public async Task Subscription_ConfirmsBeforeRestockEmailAndCompletesOnlyOnce()
    {
        var mail = new FakeMail(); var token = await SubscribeAsync(mail);
        await ProcessAsync(mail); await mail.Subjects.Count.Should().BeEqualTo(1);
        await RestockAsync(); await (await ProcessAsync(mail)).Should().BeFalse();
        await (await ReadAsync(token)).Status.Should().BeEqualTo(StockNotificationStatus.Pending);
        await ConfirmAsync(token); await ProcessAsync(mail);
        await mail.Subjects.Count.Should().BeEqualTo(2);
        await mail.Subjects.Last().StartsWith("Back in stock:").Should().BeTrue();
        await (await ReadAsync(token)).Status.Should().BeEqualTo(StockNotificationStatus.Sent);
        await (await ProcessAsync(mail)).Should().BeFalse();
    }

    [Test]
    public async Task Subscription_ConcurrentDuplicatesUseOneNormalizedRequest()
    {
        var mail = new FakeMail();
        await using (var scope = CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryItems.Where(i => i.ProductVariantId == 1).ExecuteUpdateAsync(s => s.SetProperty(i => i.QuantityOnHand, 0));
        async Task Subscribe(string email)
        {
            await using var scope = CreateScope(); await Service(scope.ServiceProvider.GetRequiredService<AppDbContext>(), mail).SubscribeAsync(1, email);
        }
        await Task.WhenAll(Subscribe("SHOPPER@example.test"), Subscribe("shopper@example.test"));
        await using var verify = CreateScope(); await (await verify.ServiceProvider.GetRequiredService<AppDbContext>().StockNotifications.CountAsync()).Should().BeEqualTo(1);
    }

    [Test]
    public async Task Subscription_UnsubscribePreventsDeliveryAndAllowsNewRequest()
    {
        var mail = new FakeMail(); var token = await SubscribeAsync(mail); await ConfirmAsync(token);
        await using (var scope = CreateScope()) await Service(scope.ServiceProvider.GetRequiredService<AppDbContext>(), mail).ChangePreferenceAsync(token, false);
        await RestockAsync(); await (await ProcessAsync(mail)).Should().BeFalse();
        await (await ReadAsync(token)).Status.Should().BeEqualTo(StockNotificationStatus.Unsubscribed);
        await using var another = CreateScope(); var db = another.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.InventoryItems.Where(i => i.ProductVariantId == 1).ExecuteUpdateAsync(s => s.SetProperty(i => i.QuantityOnHand, 0));
        await Service(db, mail).SubscribeAsync(1, "shopper@example.test");
        await (await db.StockNotifications.CountAsync()).Should().BeEqualTo(2);
    }

    [Test]
    public async Task Worker_ConcurrentClaimsDoNotSendSameRequestTwice()
    {
        var mail = new FakeMail { Entered = new(), Release = new() }; await SubscribeAsync(mail);
        var processing = ProcessAsync(mail);
        await mail.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { await (await ProcessAsync(new FakeMail())).Should().BeFalse(); }
        finally { mail.Release.TrySetResult(); }
        await processing; await mail.Subjects.Count.Should().BeEqualTo(1);
    }

    [Test]
    public async Task Worker_ExpiredLeaseRecoversAndFailedMailRemainsRetryable()
    {
        var mail = new FakeMail { Succeeds = false }; var token = await SubscribeAsync(mail);
        await using (var scope = CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().StockNotifications.Where(x => x.Token == token)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(-1)).SetProperty(x => x.LeaseToken, Guid.NewGuid()));
        await ProcessAsync(mail);
        var failed = await ReadAsync(token); await failed.Attempts.Should().BeEqualTo(1); await failed.ConfirmationSentAt.Should().BeNull();
        await (await ProcessAsync(mail)).Should().BeFalse();
        await using (var scope = CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().StockNotifications.Where(x => x.Token == token)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        mail.Succeeds = true; await ProcessAsync(mail);
        var sent = await ReadAsync(token); await sent.ConfirmationSentAt.HasValue.Should().BeTrue(); await sent.Attempts.Should().BeEqualTo(0);
    }

    [Test]
    public async Task Worker_MailUnavailableDoesNotClaimOrPretendToSend()
    {
        var mail = new FakeMail(); var token = await SubscribeAsync(mail); mail.IsConfigured = false;
        await (await ProcessAsync(mail)).Should().BeFalse(); await mail.Subjects.Count.Should().BeEqualTo(0);
        await (await ReadAsync(token)).LeaseUntil.Should().BeNull();
    }

    [Test]
    public async Task Worker_UnsubscribeWhileSendingCannotReactivateRequest()
    {
        var mail = new FakeMail { Entered = new(), Release = new() }; var token = await SubscribeAsync(mail);
        var processing = ProcessAsync(mail); await mail.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var scope = CreateScope(); await Service(scope.ServiceProvider.GetRequiredService<AppDbContext>(), new FakeMail()).ChangePreferenceAsync(token, false);
        }
        finally { mail.Release.TrySetResult(); }
        await processing; await (await ReadAsync(token)).Status.Should().BeEqualTo(StockNotificationStatus.Unsubscribed);
    }
}
