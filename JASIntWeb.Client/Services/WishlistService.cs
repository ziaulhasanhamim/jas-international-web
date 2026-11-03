using System.Text.Json;
using JASIntWeb.Client.Dtos;
using Microsoft.JSInterop;

namespace JASIntWeb.Client.Services;

public sealed class WishlistService(IJSRuntime js)
{
    private const string StorageKey = "jas.wishlist.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly List<ProductSummaryDto> _items = [];
    private bool _loaded;

    public IReadOnlyList<ProductSummaryDto> Items => _items;
    public int Count => _items.Count;
    public event Action? OnChange;

    public bool IsInWishlist(int productId) => _items.Any(i => i.Id == productId);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var parsed = JsonSerializer.Deserialize<List<ProductSummaryDto>>(raw, Json);
                if (parsed != null)
                {
                    _items.Clear();
                    _items.AddRange(parsed);
                }
            }
            _loaded = true;
            OnChange?.Invoke();
        }
        catch (InvalidOperationException)
        {
            // Prerendering: JS interop not available yet
        }
        catch (JSException)
        {
        }
    }

    public async Task ToggleWishlistAsync(ProductSummaryDto product)
    {
        await EnsureLoadedAsync();
        var existing = _items.FirstOrDefault(i => i.Id == product.Id);
        if (existing != null)
        {
            _items.Remove(existing);
        }
        else
        {
            _items.Insert(0, product);
        }

        await PersistAsync();
        OnChange?.Invoke();
    }

    public async Task RemoveAsync(int productId)
    {
        await EnsureLoadedAsync();
        var removed = _items.RemoveAll(i => i.Id == productId);
        if (removed > 0)
        {
            await PersistAsync();
            OnChange?.Invoke();
        }
    }

    public async Task ClearAsync()
    {
        _items.Clear();
        await PersistAsync();
        OnChange?.Invoke();
    }

    private async Task PersistAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_items, Json);
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSException)
        {
        }
    }
}
