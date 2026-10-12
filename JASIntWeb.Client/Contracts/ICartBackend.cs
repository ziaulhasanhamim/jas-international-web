using JASIntWeb.Client.Dtos;
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

public sealed class CommerceException(string message) : Exception(message);
