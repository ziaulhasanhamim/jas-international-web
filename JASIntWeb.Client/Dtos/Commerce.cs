using System.ComponentModel.DataAnnotations;
namespace JASIntWeb.Client.Dtos;

public sealed record CartLineRequest(int VariantId, int Quantity);

public sealed record CartMergeRequest(Guid Token, List<CartLineRequest> Items);

public sealed record CartItemDto(int VariantId, string Name, string Slug, string Image, string Variant,
    decimal Price, int Quantity, int Stock, bool IsAvailable)
{
    public decimal LineTotal => Price * Quantity;
}

public sealed record CartDto(bool IsAuthenticated, List<CartItemDto> Items)
{
    public decimal Subtotal => Items.Sum(x => x.LineTotal);
}

public sealed class CheckoutRequest
{
    public Guid RequestId { get; set; }
    public decimal? ExpectedTotal { get; set; }
    public List<CartLineRequest> Items { get; set; } = [];
    [Required, StringLength(100)]
    public string CustomerName { get; set; } = "";
    [Required, RegularExpression(@"(?:\+?88)?01[3-9]\d{8}", ErrorMessage = "Enter a valid Bangladesh mobile number.")]
    public string PhoneNumber { get; set; } = "";
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }
    [Required, StringLength(300)]
    public string Address { get; set; } = "";
    public string Division { get; set; } = "Khulna";
    [Required, StringLength(80)]
    public string District { get; set; } = "Khulna";
    public string Thana { get; set; } = "Khulna Sadar";
    [Required, StringLength(80)]
    public string City { get; set; } = "Khulna";
    public string? CouponCode { get; set; }
    public string PaymentMethod { get; set; } = "COD";
    public string? SenderPhoneNumber { get; set; }
    public string? TransactionId { get; set; }
    public string? Fbclid { get; set; }
    public string? Fbp { get; set; }
    public string? Fbc { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? UtmContent { get; set; }
    public string? UtmTerm { get; set; }
}

public sealed record MetaAttributionDto(
    string? Fbclid = null,
    string? Fbp = null,
    string? Fbc = null,
    string? UtmSource = null,
    string? UtmMedium = null,
    string? UtmCampaign = null,
    string? UtmContent = null,
    string? UtmTerm = null
);

public sealed record CheckoutResult(int OrderId, Guid PublicToken, decimal Total, string? PaymentReferenceCode = null, decimal DeliveryCharge = 0, decimal DiscountAmount = 0);

public sealed record ConfirmPaymentRequest(string PaymentMethod, string? SenderPhoneNumber, string? TransactionId, string? ReferenceCode);

public sealed record DeliveryRatesDto(decimal Khulna, decimal OutsideKhulna);

public sealed record AccountDto(bool IsAuthenticated, string? Email);

public sealed record AntiforgeryDto(string Token);

public sealed record ApiErrorDto(string? Message, string? Code);

public sealed class LocalCartDto
{
    public Guid Token { get; set; } = Guid.NewGuid();
    public Guid? CheckoutToken { get; set; }
    public List<CartLineRequest> Items { get; set; } = [];
}
// Form-mappable lines have writable properties and a public parameterless constructor.

public sealed class CheckoutLineForm
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}

public sealed class CheckoutPageForm
{
    public CheckoutRequest Details { get; set; } = new();
    public List<CheckoutLineForm> Lines { get; set; } = [];
    public string Intent { get; set; } = "review";
    public decimal? ReviewedTotal { get; set; }
}

public sealed class ReviewFormModel
{
    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [Required]
    public string Name { get; set; } = "";

    public string? CustomerName
    {
        get => Name;
        set => Name = value ?? "";
    }

    [EmailAddress]
    public string? Email { get; set; }

    public string? CustomerEmail
    {
        get => Email;
        set => Email = value;
    }

    public string? Title { get; set; }

    [Required]
    public string Comment { get; set; } = "";
}
