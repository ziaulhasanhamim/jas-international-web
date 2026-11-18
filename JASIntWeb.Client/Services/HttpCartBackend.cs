using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;

namespace JASIntWeb.Client.Services;

// Only the WebAssembly registration uses this transport. Same-origin browser cookies are automatic.
public sealed class HttpCartBackend(IHttpClientFactory factory) : ICartBackend
{
    private string? _cachedCsrfToken;

    private async Task<string> EnsureCsrfTokenAsync(HttpClient http)
    {
        if (!string.IsNullOrWhiteSpace(_cachedCsrfToken))
        {
            return _cachedCsrfToken;
        }

        var security = await http.GetFromJsonAsync<AntiforgeryDto>("api/cart/security")
            ?? throw new CommerceException("Refresh the page and try again.");
        _cachedCsrfToken = security.Token;
        return _cachedCsrfToken;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var http = factory.CreateClient("Shop");

        async Task<HttpRequestMessage> CreateRequestAsync(bool forceRefreshToken = false)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType());
            }

            if (method != HttpMethod.Get)
            {
                if (forceRefreshToken)
                {
                    _cachedCsrfToken = null;
                }
                var token = await EnsureCsrfTokenAsync(http);
                request.Headers.Add("X-CSRF-TOKEN", token);
            }

            return request;
        }

        using var initialRequest = await CreateRequestAsync();
        using var response = await http.SendAsync(initialRequest);

        // Auto-retry once on 400/403 antiforgery invalidation
        if (method != HttpMethod.Get && (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Forbidden))
        {
            using var retryRequest = await CreateRequestAsync(forceRefreshToken: true);
            using var retryResponse = await http.SendAsync(retryRequest);
            return await HandleResponseAsync<T>(retryResponse);
        }

        return await HandleResponseAsync<T>(response);
    }

    private static async Task<T> HandleResponseAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>()
                ?? throw new CommerceException("The server returned an empty response.");
        }

        ApiErrorDto? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        }
        catch (System.Text.Json.JsonException)
        {
        }

        throw new CommerceException(error?.Message ?? (response.StatusCode == HttpStatusCode.Unauthorized ? "Please sign in again." : "The request failed. Please try again."));
    }

    public Task<AccountDto> AccountAsync() => SendAsync<AccountDto>(HttpMethod.Get, "api/cart/account");

    public Task<CartDto> GetAsync() => SendAsync<CartDto>(HttpMethod.Get, "api/cart");

    public Task<CartDto> ResolveAsync(List<CartLineRequest> items) => SendAsync<CartDto>(HttpMethod.Post, "api/cart/resolve", items);

    public Task<CartDto> MergeAsync(CartMergeRequest request) => SendAsync<CartDto>(HttpMethod.Post, "api/cart/merge", request);

    public Task<CartDto> ChangeAsync(CartLineRequest line, bool add) => SendAsync<CartDto>(add ? HttpMethod.Post : HttpMethod.Put, "api/cart", line);

    public Task<DeliveryRatesDto> RatesAsync() => SendAsync<DeliveryRatesDto>(HttpMethod.Get, "api/cart/delivery-rates");
}
