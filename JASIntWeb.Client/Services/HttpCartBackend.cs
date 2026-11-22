using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;

namespace JASIntWeb.Client.Services;

// Only the WebAssembly registration uses this transport. Same-origin browser cookies are automatic.
public sealed class HttpCartBackend(IHttpClientFactory factory) : ICartBackend
{
    private string? _cachedCsrfToken;

    private async Task<Result<string>> EnsureCsrfTokenAsync(HttpClient http)
    {
        if (!string.IsNullOrWhiteSpace(_cachedCsrfToken))
        {
            return _cachedCsrfToken;
        }

        var security = await http.GetFromJsonAsync<AntiforgeryDto>("api/cart/security");
        if (security is null || string.IsNullOrWhiteSpace(security.Token))
        {
            return new CommerceError("Refresh the page and try again.", 400);
        }

        _cachedCsrfToken = security.Token;
        return _cachedCsrfToken;
    }

    private async Task<Result<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var http = factory.CreateClient("Shop");

        async Task<Result<HttpRequestMessage>> CreateRequestAsync(bool forceRefreshToken = false)
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
                var tokenResult = await EnsureCsrfTokenAsync(http);
                if (tokenResult switch { string => false, Error => true })
                {
                    tokenResult.TryGet(out _, out var tokenErr);
                    return tokenErr!;
                }
                request.Headers.Add("X-CSRF-TOKEN", tokenResult.ValueOrDefault()!);
            }

            return request;
        }

        var initialRequestResult = await CreateRequestAsync();
        if (initialRequestResult switch { HttpRequestMessage => false, Error => true })
        {
            initialRequestResult.TryGet(out _, out var initErr);
            return initErr!;
        }
        using var initialRequest = initialRequestResult.ValueOrDefault()!;
        using var response = await http.SendAsync(initialRequest);

        // Auto-retry once on 400/403 antiforgery invalidation
        if (method != HttpMethod.Get && (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Forbidden))
        {
            var retryRequestResult = await CreateRequestAsync(forceRefreshToken: true);
            if (retryRequestResult switch { HttpRequestMessage => false, Error => true })
            {
                retryRequestResult.TryGet(out _, out var retryErr);
                return retryErr!;
            }
            using var retryRequest = retryRequestResult.ValueOrDefault()!;
            using var retryResponse = await http.SendAsync(retryRequest);
            return await HandleResponseAsync<T>(retryResponse);
        }

        return await HandleResponseAsync<T>(response);
    }

    private static async Task<Result<T>> HandleResponseAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadFromJsonAsync<T>();
            if (content is null)
            {
                return new CommerceError("The server returned an empty response.", 500);
            }
            return content;
        }

        ApiErrorDto? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        }
        catch (System.Text.Json.JsonException)
        {
        }

        var message = error?.Message ?? (response.StatusCode == HttpStatusCode.Unauthorized ? "Please sign in again." : "The request failed. Please try again.");
        return new CommerceError(message, (int)response.StatusCode);
    }

    public Task<Result<AccountDto>> AccountAsync() => SendAsync<AccountDto>(HttpMethod.Get, "api/cart/account");

    public Task<Result<CartDto>> GetAsync() => SendAsync<CartDto>(HttpMethod.Get, "api/cart");

    public Task<Result<CartDto>> ResolveAsync(List<CartLineRequest> items) => SendAsync<CartDto>(HttpMethod.Post, "api/cart/resolve", items);

    public Task<Result<CartDto>> MergeAsync(CartMergeRequest request) => SendAsync<CartDto>(HttpMethod.Post, "api/cart/merge", request);

    public Task<Result<CartDto>> ChangeAsync(CartLineRequest line, bool add) => SendAsync<CartDto>(add ? HttpMethod.Post : HttpMethod.Put, "api/cart", line);

    public Task<Result<DeliveryRatesDto>> RatesAsync() => SendAsync<DeliveryRatesDto>(HttpMethod.Get, "api/cart/delivery-rates");
}
