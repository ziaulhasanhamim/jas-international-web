using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;

namespace JASIntWeb.Client.Contracts;

public interface ICartBackend
{
    Task<AccountDto> AccountAsync();

    Task<CartDto> GetAsync();

    Task<CartDto> ResolveAsync(List<CartLineRequest> items);

    Task<CartDto> MergeAsync(CartMergeRequest request);

    Task<CartDto> ChangeAsync(CartLineRequest line, bool add);

    Task<DeliveryRatesDto> RatesAsync();
}

public sealed class CommerceException : Exception
{
    public CommerceError Error { get; }
    public CommerceException(string message, int code = 400) : base(message) => Error = new CommerceError(message, code);
    public CommerceException(CommerceError error) : base(error.Message) => Error = error;
    public static implicit operator CommerceError(CommerceException ex) => ex.Error;
    public static implicit operator CommerceException(CommerceError error) => new(error);
}
