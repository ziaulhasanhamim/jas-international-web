using System.Security.Cryptography;
using System.Text;
using JASIntWeb.Helpers;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Http.HttpResults;
namespace JASIntWeb.Endpoints;

using WebhookResult = Results<Ok<PaymentWebhookResponse>, BadRequest<EndpointError>, UnauthorizedHttpResult,
    Conflict<EndpointError>, JsonHttpResult<EndpointError>>;

public static class PaymentWebhookEndpoints
{
    extension (IEndpointRouteBuilder endpoints)
    {
        public void MapPaymentWebhookEndpoints() =>
            endpoints.MapPost("/api/webhooks/mfs-sms", ReceiveSms)
                .DisableAntiforgery()
                .WithSummary("Verify a mobile-money payment from an authenticated SMS forwarder.");
    }

    private static async Task<WebhookResult> ReceiveSms(
        HttpRequest request, AppDbContext db, ShoppingService shop, DeliverySettingsService settings,
        ILoggerFactory logs, CancellationToken ct)
    {
        var result = await PaymentNotificationReader.ReadAsync(request, ct);
        return result switch
        {
            PaymentNotification notification => await VerifyAsync(notification, db, shop, settings, logs, ct),
            InvalidPaymentNotification error => TypedResults.BadRequest(new EndpointError(error.Message))
        };
    }

    private static async Task<WebhookResult> VerifyAsync(
        PaymentNotification notification, AppDbContext db, ShoppingService shop,
        DeliverySettingsService settingsService, ILoggerFactory logs, CancellationToken ct)
    {
        var settings = await settingsService.GetSettingsAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.SmsWebhookSecret)
            || settings.SmsWebhookSecret is "jas_sms_secret_2026" or "jas_secret_sms_2026")
            return TypedResults.Json(new EndpointError("Payment webhook is disabled."), statusCode: StatusCodes.Status503ServiceUnavailable);

        if (!HasValidSecret(settings.SmsWebhookSecret, notification.Secret)) return TypedResults.Unauthorized();
        if (notification.Amount is <= 0 or > 10_000_000 || notification.TransactionId.Length < 6)
            return TypedResults.BadRequest(new EndpointError("A positive amount and valid transaction ID are required."));

        var matches = await db.Orders.AsNoTracking()
            .Where(order => (notification.Reference != "" && order.PaymentReferenceCode == notification.Reference)
                || order.TransactionId == notification.TransactionId)
            .Select(order => order.Id).Take(2).ToListAsync(ct);
        if (matches is not [var orderId])
            return TypedResults.Conflict(new EndpointError("No unambiguous order match. Review this payment manually."));

        var verifyResult = await shop.TryVerifyPaymentAsync(orderId, notification.Amount, notification.TransactionId);
        return verifyResult switch
        {
            Order order => OnPaymentVerified(order, logs),
            ValidationError validation => TypedResults.BadRequest(new EndpointError(validation.Message)),
            Error error => TypedResults.Conflict(new EndpointError(error.Message))
        };
    }

    private static WebhookResult OnPaymentVerified(Order order, ILoggerFactory logs)
    {
        logs.CreateLogger(nameof(PaymentWebhookEndpoints)).LogInformation("Payment verified for order {OrderId}.", order.Id);
        return TypedResults.Ok(new PaymentWebhookResponse(true, true, order.Id, order.PaymentReferenceCode,
            order.TransactionId, order.PaidAmount, order.Status.ToString(), "Verified",
            "Payment verified. Fulfillment progress is managed separately by the store."));
    }

    private static bool HasValidSecret(string expected, string supplied) => CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
}
