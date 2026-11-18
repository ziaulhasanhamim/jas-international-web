using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JASIntWeb.Client.Services;

// One snapshot is transferred from prerendering to either interactive runtime.
public sealed record CartState(CartDto? Cart, string? Error = null);

public sealed class CartService(IJSRuntime js, ICartBackend backend)
{
    private readonly SemaphoreSlim gate = new(1, 1);

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

    // Check inside the gate so concurrent consumers share one initial load,
    // including an empty cart or a failed load that requires an explicit retry.
    public Task InitializeAsync() => RunAsync(backend.GetAsync, initializeOnly: true, syncBadge: false);

    public Task RefreshAsync() => RunAsync(backend.GetAsync);

    public Task AddAsync(int variantId, int quantity = 1) => ChangeAsync(variantId, quantity, true);

    public Task SetQuantityAsync(int variantId, int quantity) => ChangeAsync(variantId, quantity, false);

    private Task ChangeAsync(int variantId, int quantity, bool add) =>
        RunAsync(() => backend.ChangeAsync(new CartLineRequest(variantId, quantity), add));

    private async Task RunAsync(Func<Task<CartDto>> action, bool initializeOnly = false, bool syncBadge = true)
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
                // Keep the last confirmed cart visible until the server accepts the change.
                State = new CartState(await action());
            }
            catch (Exception e) when (e is CommerceException or HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                var message = e is HttpRequestException or TaskCanceledException
                    ? "Connection interrupted. Please try again."
                    : e.Message;
                State = new CartState(State?.Cart, message);
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
