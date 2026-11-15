using System.Text.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using Microsoft.JSInterop;
namespace JASIntWeb.Client.Services;
// State and notifications stay in C#. JS interop only reads/writes browser localStorage.

public sealed class CartService(IJSRuntime js, ICartBackend backend)
{
    private const string StorageKey = "jas.cart.v2";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool loaded;
    public IReadOnlyList<CartItemDto> Items { get; private set; } = [];
    public bool IsAuthenticated { get; private set; }
    public string? Error { get; private set; }
    public int ItemCount => Items.Sum(i => i.Quantity);
    public decimal Total => Items.Sum(i => i.LineTotal);
    public event Action? OnChange;

    private async Task<LocalCartDto> ReadAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            var cart = json is null ? null : JsonSerializer.Deserialize<LocalCartDto>(json, Json);
            if (cart != null && cart.Token != Guid.Empty && cart.Items != null && cart.Items.Count <= 100)
            {
                cart.Items = cart.Items.Where(i => i.VariantId > 0 && i.Quantity is > 0 and <= 99).ToList();
                if (cart.Items.Count > 0)
                {
                    return cart;
                }
            }

            // Fallback: Check if guest cart cookie was set via SSR form submission
            try
            {
                var cookieJson = await js.InvokeAsync<string?>("eval",
                    "(function(){ const m = document.cookie.match(/(?:^|;\\s*)jas_guest_cart=([^;]+)/); return m ? decodeURIComponent(m[1]) : null; })()");
                if (!string.IsNullOrWhiteSpace(cookieJson))
                {
                    var guestItems = JsonSerializer.Deserialize<List<CartLineRequest>>(cookieJson, Json);
                    if (guestItems != null && guestItems.Count > 0)
                    {
                        var newCart = new LocalCartDto
                        {
                            Token = Guid.NewGuid(),
                            Items = guestItems.Where(i => i.VariantId > 0 && i.Quantity is > 0 and <= 99).ToList()
                        };
                        await WriteAsync(newCart);
                        return newCart;
                    }
                }
            }
            catch {}

            return new();
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            return new();
        }
    }

    private async ValueTask WriteAsync(LocalCartDto cart)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(cart, Json));
            await js.InvokeVoidAsync("window.jasUpdateCartCount", cart.Items.Sum(i => i.Quantity));
        }
        catch (JSException)
        {
        }
    }

    private async Task ClearIfTokenAsync(Guid token)
    {
        var current = await ReadAsync();
        if (current.Token == token)
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            }
            catch (JSException)
            {
            }
        }
    }

    private void Apply(CartDto cart)
    {
        Items = cart.Items;
        IsAuthenticated = cart.IsAuthenticated;
        Error = null;
        loaded = true;
        try
        {
            _ = js.InvokeVoidAsync("window.jasUpdateCartCount", Items.Sum(i => i.Quantity));
        }
        catch
        {
        }
    }

    private async Task<LocalCartDto> IdentityAndMergeAsync()
    {
        var account = await backend.AccountAsync();
        if (IsAuthenticated != account.IsAuthenticated)
        {
            Items = [];
            loaded = false;
        }
        IsAuthenticated = account.IsAuthenticated;
        var local = await ReadAsync();
        if (IsAuthenticated && local.Items.Count > 0)
        {
            Apply(await backend.MergeAsync(new(local.Token, local.Items)));
            await ClearIfTokenAsync(local.Token);
            local = await ReadAsync();
        }
        return local;
    }

    private async Task RunAsync(Func<Task> action)
    {
        await gate.WaitAsync();
        try
        {
            Error = null;
            await action();
        }
        catch (Exception e) when (e is JSException or CommerceException or HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            Error = e is HttpRequestException or TaskCanceledException ? "Connection interrupted. Please try again." : e.Message;
        }
        finally
        {
            gate.Release();
            OnChange?.Invoke();
        }
    }

    public Task InitializeAsync() => RunAsync(async () =>
        {
            if (loaded)
            {
                return;
            }
            var local = await IdentityAndMergeAsync();
            Apply(IsAuthenticated ? await backend.GetAsync() : await backend.ResolveAsync(local.Items));
        });

    public Task RefreshAsync() => RunAsync(async () =>
        {
            var local = await IdentityAndMergeAsync();
            Apply(IsAuthenticated ? await backend.GetAsync() : await backend.ResolveAsync(local.Items));
        });

    public Task AddAsync(int variantId, int quantity = 1) => ChangeAsync(variantId, quantity, true);

    public Task SetQuantityAsync(int variantId, int quantity) => ChangeAsync(variantId, quantity, false);

    private Task ChangeAsync(int variantId, int quantity, bool add) => RunAsync(async () =>
        {
            // Optimistic in-memory update for 0ms instantaneous UI response
            var existingItem = Items.FirstOrDefault(i => i.VariantId == variantId);
            if (existingItem != null)
            {
                var optimisticQty = add ? existingItem.Quantity + quantity : quantity;
                if (optimisticQty <= 0)
                {
                    Items = Items.Where(i => i.VariantId != variantId).ToList();
                }
                else
                {
                    var updatedItem = existingItem with { Quantity = optimisticQty };
                    Items = Items.Select(i => i.VariantId == variantId ? updatedItem : i).ToList();
                }
                OnChange?.Invoke();
            }

            var local = await IdentityAndMergeAsync();
            if (IsAuthenticated)
            {
                Apply(await backend.ChangeAsync(new(variantId, quantity), add));
            }
            else
            {
                var old = local.Items.Where(i => i.VariantId == variantId).Sum(i => i.Quantity);
                var next = add ? old + quantity : quantity;
                if (next < 0 || next > 99 || (add && quantity <= 0))
                {
                    throw new CommerceException("Quantity must be between 1 and 99.");
                }
                var lines = local.Items.Where(i => i.VariantId != variantId).ToList();
                if (next > 0)
                {
                    lines.Add(new(variantId, next));
                }
                var result = await backend.ResolveAsync(lines);
                var changed = result.Items.FirstOrDefault(i => i.VariantId == variantId);
                if (changed is not null && (!changed.IsAvailable || changed.Quantity > changed.Stock))
                {
                    throw new CommerceException("The requested quantity is unavailable.");
                }
                local.Items = lines;
                local.Token = Guid.NewGuid();
                local.CheckoutToken = null;
                await WriteAsync(local);
                Apply(result);
                return;
            }
            local.CheckoutToken = null;
            await WriteAsync(local);
        });

    public async Task<Guid> CheckoutTokenAsync()
    {
        await gate.WaitAsync();
        try
        {
            var local = await ReadAsync();
            local.CheckoutToken ??= Guid.NewGuid();
            await WriteAsync(local);
            return local.CheckoutToken.Value;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task CompleteCheckoutAsync(Guid checkoutToken) => RunAsync(async () =>
        {
            var local = await ReadAsync();
            if (local.CheckoutToken == checkoutToken)
            {
                await ClearIfTokenAsync(local.Token);
            }
            var remaining = await IdentityAndMergeAsync();
            Apply(IsAuthenticated ? await backend.GetAsync() : await backend.ResolveAsync(remaining.Items));
        });
}
