using System.Net;
using System.Net.Http.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
namespace JASIntWeb.Client.Services;
// Only the WebAssembly registration uses this transport. Same-origin browser cookies are automatic.

public sealed class HttpCartBackend(IHttpClientFactory factory) : ICartBackend
{
    private string? token;

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var http = factory.CreateClient("Shop");
        for (var attempt = 0;; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            if (method != HttpMethod.Get)
            {
                token ??= (await http.GetFromJsonAsync<AntiforgeryDto>("api/cart/security"))?.Token
                ?? throw new CommerceException("Could not refresh your session. Reload the page.");
                request.Headers.Add("X-CSRF-TOKEN", token);
            }
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType());
            }
            using var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>() ?? throw new CommerceException("The server returned an empty response.");
            }
            ApiErrorDto? error = null;
            try
            {
                error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
            }
            catch (System.Text.Json.JsonException)
            {
            }
            if (response.StatusCode == HttpStatusCode.BadRequest && error?.Code == "antiforgery" && attempt == 0)
            {
                token = null;
                continue;
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                token = null;
            }
            throw new CommerceException(error?.Message ?? (response.StatusCode == HttpStatusCode.Unauthorized ? "Please sign in again." : "The request failed. Please try again."));
        }
    }

    public Task<AccountDto> AccountAsync() => SendAsync<AccountDto>(HttpMethod.Get, "api/cart/account");

    public Task<CartDto> GetAsync() => SendAsync<CartDto>(HttpMethod.Get, "api/cart");

    public Task<CartDto> ResolveAsync(List<CartLineRequest> items) => SendAsync<CartDto>(HttpMethod.Post, "api/cart/resolve", items);

    public Task<CartDto> MergeAsync(CartMergeRequest request) => SendAsync<CartDto>(HttpMethod.Post, "api/cart/merge", request);

    public Task<CartDto> ChangeAsync(CartLineRequest line, bool add) => SendAsync<CartDto>(add ? HttpMethod.Post : HttpMethod.Put, "api/cart", line);

    public Task<DeliveryRatesDto> RatesAsync() => SendAsync<DeliveryRatesDto>(HttpMethod.Get, "api/cart/delivery-rates");
}
