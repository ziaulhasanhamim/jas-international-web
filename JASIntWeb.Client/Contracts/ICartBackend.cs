using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;

namespace JASIntWeb.Client.Contracts;

public interface ICartBackend
{
    Task<Result<AccountDto>> AccountAsync();

    Task<Result<CartDto>> GetAsync();

    Task<Result<CartDto>> ResolveAsync(List<CartLineRequest> items);

    Task<Result<CartDto>> MergeAsync(CartMergeRequest request);

    Task<Result<CartDto>> ChangeAsync(CartLineRequest line, bool add);

    Task<Result<DeliveryRatesDto>> RatesAsync();
}
