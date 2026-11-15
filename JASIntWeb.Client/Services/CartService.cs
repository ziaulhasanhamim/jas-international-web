using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using Microsoft.JSInterop;

namespace JASIntWeb.Client.Services;

public sealed class CartService(IJSRuntime js, ICartBackend backend)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public IReadOnlyList<CartItemDto> Items { get; private set; } = [];
    public bool IsAuthenticated { get; private set; }
    public string? Error { get; private set; }
    public int ItemCount => Items.Sum(i => i.Quantity);
    public decimal Total => Items.Sum(i => i.LineTotal);
    public event Action? OnChange;

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
            _ = UpdateDomBadgeAsync();
        }
    }

    private void Apply(CartDto cart)
    {
        Items = cart.Items;
        IsAuthenticated = cart.IsAuthenticated;
        Error = null;
    }

    private async ValueTask UpdateDomBadgeAsync()
    {
        try
        {
            await js.InvokeVoidAsync("window.jasUpdateCartCount", ItemCount);
        }
        catch
        {
        }
    }

    public Task InitializeAsync() => RefreshAsync();

    public Task RefreshAsync() => RunAsync(async () =>
    {
        var cart = await backend.GetAsync();
        Apply(cart);
    });

    public Task AddAsync(int variantId, int quantity = 1) => ChangeAsync(variantId, quantity, true);

    public Task SetQuantityAsync(int variantId, int quantity) => ChangeAsync(variantId, quantity, false);

    private Task ChangeAsync(int variantId, int quantity, bool add) => RunAsync(async () =>
    {
        // Optimistic in-memory update for fast UI response
        var existing = Items.FirstOrDefault(i => i.VariantId == variantId);
        if (existing != null)
        {
            var nextQty = add ? existing.Quantity + quantity : quantity;
            if (nextQty <= 0)
            {
                Items = Items.Where(i => i.VariantId != variantId).ToList();
            }
            else
            {
                var updated = existing with { Quantity = nextQty };
                Items = Items.Select(i => i.VariantId == variantId ? updated : i).ToList();
            }
            OnChange?.Invoke();
            _ = UpdateDomBadgeAsync();
        }

        var result = await backend.ChangeAsync(new CartLineRequest(variantId, quantity), add);
        Apply(result);
    });

    public Task<Guid> CheckoutTokenAsync() => Task.FromResult(Guid.NewGuid());

    public Task CompleteCheckoutAsync(Guid checkoutToken) => RunAsync(async () =>
    {
        var cart = await backend.GetAsync();
        Apply(cart);
    });
}
