using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JASIntWeb.Services;

internal sealed record PaymentNotification(string Secret, decimal Amount, string TransactionId, string Reference);
internal sealed record InvalidPaymentNotification(string Message);
internal union PaymentNotificationReadResult(PaymentNotification, InvalidPaymentNotification);

internal static partial class PaymentNotificationReader
{
    public static async Task<PaymentNotificationReadResult> ReadAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength > 16 * 1024)
            return new InvalidPaymentNotification("Payment notification is too large.");
        try
        {
            var fields = request.HasJsonContentType()
                ? await ReadJsonAsync(request, ct)
                : request.HasFormContentType
                    ? await ReadFormAsync(request, ct)
                    : [];

            if (fields.Count == 0)
                return new InvalidPaymentNotification("Send a JSON or form payment notification.");

            var message = Field(fields, "message", "text", "sms");
            var secret = Field(fields, "secret");
            var transactionId = Field(fields, "trxId", "trx_id", "transactionId");
            var reference = Field(fields, "reference", "ref");
            var amount = ParseAmount(Field(fields, "amount"));

            if (!string.IsNullOrWhiteSpace(message))
            {
                if (amount == 0)
                {
                    var match = AmountPattern().Match(message);
                    amount = ParseAmount(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
                }
                if (reference.Length == 0) reference = ReferencePattern().Match(message).Groups[1].Value;
                if (transactionId.Length == 0) transactionId = TransactionPattern().Match(message).Groups[1].Value;
            }

            return new PaymentNotification(secret, amount, transactionId.Trim().ToUpperInvariant(), reference.Trim());
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException or RegexMatchTimeoutException)
        {
            return new InvalidPaymentNotification("Invalid payment notification.");
        }
    }

    private static async Task<Dictionary<string, string>> ReadJsonAsync(HttpRequest request, CancellationToken ct)
    {
        using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Expected a JSON object.");

        Dictionary<string, string> values = [];
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name is not ("secret" or "amount" or "message" or "text" or "sms" or "trxId" or "trx_id" or "transactionId" or "reference" or "ref")) continue;
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "",
                JsonValueKind.Number when property.Name == "amount" => property.Value.GetRawText(),
                JsonValueKind.Null => "",
                _ => throw new JsonException("Unsupported payment field value.")
            };
        }
        return values;
    }

    private static async Task<Dictionary<string, string>> ReadFormAsync(HttpRequest request, CancellationToken ct) =>
        (await request.ReadFormAsync(ct)).ToDictionary(field => field.Key, field => field.Value.ToString());

    private static string Field(IReadOnlyDictionary<string, string> fields, params ReadOnlySpan<string> names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        }
        return "";
    }

    private static decimal ParseAmount(string value) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var amount) ? amount : 0;

    [GeneratedRegex(@"(?:Tk\.?|BDT|amount)\s*([\d,]+(?:\.\d+)?)|\b([\d,]+(?:\.\d+)?)\s*(?:Tk|BDT)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"(?:Reference|Ref)\s*:?\s*([A-Za-z0-9\-]+)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"(?:TrxID|TxnID|Transaction ID)\s*:?\s*([A-Za-z0-9]+)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex TransactionPattern();
}
