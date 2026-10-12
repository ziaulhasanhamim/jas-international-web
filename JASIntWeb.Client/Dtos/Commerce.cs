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
    [Required, StringLength(300)]
    public string Address { get; set; } = "";
    [Required, StringLength(80)]
    public string District { get; set; } = "";
    [Required, StringLength(80)]
    public string City { get; set; } = "";
}

public sealed record CheckoutResult(int OrderId, Guid PublicToken, decimal Total);

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
