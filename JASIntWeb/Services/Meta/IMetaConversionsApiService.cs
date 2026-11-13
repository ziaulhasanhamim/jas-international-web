using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence.Entities;
using Microsoft.AspNetCore.Http;

namespace JASIntWeb.Services.Meta;

public sealed class MetaUserData
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; } = "BD";
    public string? ClientIpAddress { get; set; }
    public string? ClientUserAgent { get; set; }
    public string? Fbp { get; set; }
    public string? Fbc { get; set; }
    public string? ExternalId { get; set; }
}

public sealed class MetaCustomData
{
    public decimal? Value { get; set; }
    public string Currency { get; set; } = "BDT";
    public string? ContentType { get; set; } = "product";
    public string? ContentName { get; set; }
    public string? ContentCategory { get; set; }
    public List<string>? ContentIds { get; set; }
    public List<MetaContentItem>? Contents { get; set; }
    public int? NumItems { get; set; }
    public string? OrderId { get; set; }
}

public sealed class MetaContentItem
{
    public required string Id { get; set; }
    public int Quantity { get; set; }
    public decimal ItemPrice { get; set; }
    public string? Title { get; set; }
}

public interface IMetaConversionsApiService
{
    Task<MetaAdsConfig> GetEffectiveConfigAsync();
    Task<bool> SendPurchaseEventAsync(Order order, CheckoutRequest? request = null, HttpContext? httpContext = null, CancellationToken ct = default);
    Task<bool> SendEventAsync(string eventName, string eventId, string? eventSourceUrl, MetaUserData userData, MetaCustomData? customData = null, CancellationToken ct = default);
    Task<(bool Success, string Message)> SendTestEventAsync(string pixelId, string accessToken, string? testEventCode, CancellationToken ct = default);
}
