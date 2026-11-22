using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JASIntWeb.Client.Services;

// One snapshot is transferred from prerendering to either interactive runtime.
public sealed record CartState(CartDto? Cart, string? Error = null);

public sealed class CartService(IJSRuntime js, ICartBackend backend)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CartDto? _confirmedCart;

    [PersistentState(AllowUpdates = true)]
    public CartState? State { get; set; }

    public IReadOnlyList<CartItemDto> Items => State?.Cart?.Items ?? [];
    public bool IsAuthenticated => State?.Cart?.IsAuthenticated ?? false;
    public string? Error => State?.Error;
    public bool IsInitialized => State is not null;
    public bool HasCart => State?.Cart is not null;
    public bool IsBusy { get; private set; }
    public int ItemCount => State?.Cart?.ItemCount ?? 0;
    public decimal Total => State?.Cart?.Subtotal ?? 0;
    public event Action? OnChange;

    public Task InitializeAsync() => RunAsync(backend.GetAsync, initializeOnly: true, syncBadge: false);

    public Task RefreshAsync() => RunAsync(backend.GetAsync);

    public Task AddAsync(int variantId, int quantity = 1) => ChangeAsync(variantId, quantity, add: true);

    public Task SetQuantityAsync(int variantId, int quantity) => ChangeAsync(variantId, quantity, add: false);

    private async Task ChangeAsync(int variantId, int quantity, bool add)
    {
        var clamped = Math.Clamp(quantity, 0, 99);
        var currentCart = State?.Cart;

        // 1. Instant optimistic UI update (0ms perceived latency)
        if (currentCart is not null)
        {
            _confirmedCart ??= currentCart;
            var existingItem = currentCart.Items.FirstOrDefault(i => i.VariantId == variantId);
            if (existingItem != null)
            {
                var targetQuantity = add ? Math.Clamp(existingItem.Quantity + clamped, 0, 99) : clamped;
                List<CartItemDto> updatedItems;
                if (targetQuantity == 0)
                {
                    updatedItems = currentCart.Items.Where(i => i.VariantId != variantId).ToList();
                }
                else
                {
                    updatedItems = currentCart.Items.Select(i => i.VariantId == variantId
                        ? i with { Quantity = targetQuantity }
                        : i).ToList();
                }

                State = new CartState(currentCart with { Items = updatedItems }, null);
                OnChange?.Invoke();
            }
        }

        // 2. Dispatch to backend with authoritative reconciliation & error rollback
        await gate.WaitAsync();
        try
        {
            IsBusy = true;
            try
            {
                var result = await backend.ChangeAsync(new CartLineRequest(variantId, clamped), add);
                switch (result)
                {
                    case CartDto cart:
                        _confirmedCart = cart;
                        State = new CartState(cart, null);
                        break;
                    case Error err:
                        var rollbackCart = _confirmedCart ?? currentCart;
                        State = new CartState(rollbackCart, err.Message);
                        break;
                }
            }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                var message = e is HttpRequestException or TaskCanceledException
                    ? "Connection interrupted. Please try again."
                    : e.Message;

                // Roll back to last confirmed server cart on failure
                var rollbackCart = _confirmedCart ?? currentCart;
                State = new CartState(rollbackCart, message);
            }
        }
        finally
        {
            IsBusy = false;
            gate.Release();
            OnChange?.Invoke();
            await SynchronizeBadgeAsync();
        }
    }

    private async Task RunAsync(Func<Task<Result<CartDto>>> action, bool initializeOnly = false, bool syncBadge = true)
    {
        await gate.WaitAsync();
        try
        {
            if (initializeOnly && IsInitialized) return;

            IsBusy = true;
            if (State is not null) State = State with { Error = null };
            OnChange?.Invoke();
            try
            {
                var result = await action();
                switch (result)
                {
                    case CartDto cart:
                        _confirmedCart = cart;
                        State = new CartState(cart);
                        break;
                    case Error err:
                        State = new CartState(State?.Cart ?? _confirmedCart, err.Message);
                        break;
                }
            }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                var message = e is HttpRequestException or TaskCanceledException
                    ? "Connection interrupted. Please try again."
                    : e.Message;
                State = new CartState(State?.Cart ?? _confirmedCart, message);
            }
        }
        finally
        {
            IsBusy = false;
            gate.Release();
            OnChange?.Invoke();
        }

        if (syncBadge) await SynchronizeBadgeAsync();
    }

    // Called after interactive rendering, never as part of prerender initialization.
    public async ValueTask SynchronizeBadgeAsync()
    {
        if (!HasCart) return;
        try
        {
            await js.InvokeVoidAsync("window.jasUpdateCartBadge", ItemCount);
        }
        catch (Exception e) when (e is JSException or InvalidOperationException or TaskCanceledException)
        {
            // Badge enhancement must not affect a successful cart operation.
        }
    }

    public Task<Guid> CheckoutTokenAsync() => Task.FromResult(Guid.NewGuid());

    public Task CompleteCheckoutAsync(Guid checkoutToken) => RefreshAsync();
}
