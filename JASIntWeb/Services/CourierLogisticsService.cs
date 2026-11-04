namespace JASIntWeb.Services;

public sealed record CourierOption(string Code, string Name, string BadgeColor, string TrackingUrlPattern);

public sealed class CourierLogisticsService
{
    public static readonly IReadOnlyList<CourierOption> AvailableCouriers =
    [
        new("steadfast", "Steadfast Courier", "emerald", "https://steadfast.com.bd/t/{CODE}"),
        new("pathao", "Pathao Courier", "rose", "https://pathao.com/courier/tracking/?consignment_id={CODE}"),
        new("redx", "RedX Logistics", "red", "https://redx.com.bd/track-order/?trackingId={CODE}"),
        new("paperfly", "Paperfly Courier", "blue", "https://paperfly.com.bd/tracking.php?tracking_id={CODE}"),
        new("sundarban", "Sundarban Courier", "amber", "https://sundarbancourierltd.com/"),
        new("inhouse", "JAS Express (In-House Rider)", "indigo", ""),
        new("pickup", "Showroom Pickup", "slate", "")
    ];

    public string GetTrackingUrl(string? courierName, string? trackingCode)
    {
        if (string.IsNullOrWhiteSpace(trackingCode)) return "";
        var code = trackingCode.Trim();

        var match = AvailableCouriers.FirstOrDefault(c =>
            c.Name.Equals(courierName, StringComparison.OrdinalIgnoreCase) ||
            c.Code.Equals(courierName, StringComparison.OrdinalIgnoreCase));

        if (match != null && !string.IsNullOrEmpty(match.TrackingUrlPattern) && match.TrackingUrlPattern.Contains("{CODE}"))
        {
            return match.TrackingUrlPattern.Replace("{CODE}", code);
        }

        if (courierName?.Contains("steadfast", StringComparison.OrdinalIgnoreCase) == true)
            return $"https://steadfast.com.bd/t/{code}";
        if (courierName?.Contains("pathao", StringComparison.OrdinalIgnoreCase) == true)
            return $"https://pathao.com/courier/tracking/?consignment_id={code}";
        if (courierName?.Contains("redx", StringComparison.OrdinalIgnoreCase) == true)
            return $"https://redx.com.bd/track-order/?trackingId={code}";
        if (courierName?.Contains("paperfly", StringComparison.OrdinalIgnoreCase) == true)
            return $"https://paperfly.com.bd/tracking.php?tracking_id={code}";

        return "";
    }

    public string BuildCustomerSmsText(string customerName, int orderId, string? courier, string? trackingCode, string? trackingUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName.Trim();
        var courierStr = string.IsNullOrWhiteSpace(courier) ? "our delivery partner" : courier.Trim();

        if (!string.IsNullOrWhiteSpace(trackingCode) && !string.IsNullOrWhiteSpace(trackingUrl))
        {
            return $"Dear {greeting}, your JAS International order #{orderId} has been dispatched via {courierStr}. Tracking ID: {trackingCode}. Track live: {trackingUrl}. Helpline: 01711465078.";
        }

        if (!string.IsNullOrWhiteSpace(trackingCode))
        {
            return $"Dear {greeting}, your JAS International order #{orderId} has been handed over to {courierStr}. Tracking ID: {trackingCode}. Helpline: 01711465078.";
        }

        return $"Dear {greeting}, your JAS International order #{orderId} is out for delivery with {courierStr}. Thank you for shopping with us!";
    }

    public decimal CalculateCodAmountToCollect(decimal orderTotal, decimal paidAmount)
    {
        var due = orderTotal - paidAmount;
        return due > 0 ? due : 0m;
    }
}
