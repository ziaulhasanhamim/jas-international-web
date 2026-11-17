namespace JASIntWeb.Endpoints;

/// <summary>A safe error message for existing JSON clients.</summary>
public sealed record EndpointError(string Error);

/// <summary>The antiforgery token used by administrator upload clients.</summary>
public sealed record UploadSecurityResponse(string? Token);

/// <summary>The location of a newly uploaded image.</summary>
public sealed record UploadedImageResponse(string Url);

/// <summary>The locations of newly uploaded product images.</summary>
public sealed record UploadedImagesResponse(IReadOnlyList<string> Urls);

/// <summary>A confirmed mobile-money payment and its independent fulfillment state.</summary>
public sealed record PaymentWebhookResponse(
    bool Success, bool Matched, int OrderId, string? Reference, string? TrxId,
    decimal AmountPaid, string OrderStatus, string PaymentStatus, string Message);
