using System.Text.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Client.Services;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JASIntWeb.Client.Components.Cart;
using NSubstitute;

namespace JASIntWeb.Tests.Unit;

public class CartServiceTests
{
    private static CartDto Cart(int quantity = 2, bool authenticated = true) => new(authenticated,
        [new(7, "Watch", "watch", "/watch.webp", "Black", 150m, quantity, 10, true)]);


    [Test]
    public async Task CartContents_RestoredServiceRendersItemsWithoutLoadingOrRefetch()
    {
        var backend = Substitute.For<ICartBackend>();
        var service = new CartService(Substitute.For<IJSRuntime>(), backend) { State = new(Cart()) };
        await using var provider = new ServiceCollection().AddLogging().AddSingleton(service).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CartContents>();
            return component.ToHtmlString();
        });
        await html.Contains("href=\"/product/watch\"").Should().BeTrue();
        await html.Contains("Loading your cart").Should().BeFalse();
        await html.Contains("Your cart is empty").Should().BeFalse();
        await backend.DidNotReceive().GetAsync();
    }

    [Test]
    public async Task CartContents_RestoredFailureRendersRetryInsteadOfEmptyCart()
    {
        var backend = Substitute.For<ICartBackend>();
        var service = new CartService(Substitute.For<IJSRuntime>(), backend)
        {
            State = new(null, "Connection interrupted. Please try again.")
        };
        await using var provider = new ServiceCollection().AddLogging().AddSingleton(service).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CartContents>();
            return component.ToHtmlString();
        });
        await html.Contains("Retry").Should().BeTrue();
        await html.Contains("Connection interrupted").Should().BeTrue();
        await html.Contains("Your cart is empty").Should().BeFalse();
        await backend.DidNotReceive().GetAsync();
    }

    [Test]
    public async Task Initialize_ConcurrentConsumersLoadOnceAndReuseConfirmedCart()
    {
        var backend = Substitute.For<ICartBackend>();
        var pending = new TaskCompletionSource<CartDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.GetAsync().Returns(pending.Task);
        var js = Substitute.For<IJSRuntime>();
        var service = new CartService(js, backend);
        var first = service.InitializeAsync();
        var second = service.InitializeAsync();
        await service.IsBusy.Should().BeTrue();
        pending.SetResult(Cart());
        await Task.WhenAll(first, second);
        await service.InitializeAsync();
        await backend.Received(1).GetAsync();
        await service.ItemCount.Should().BeEqualTo(2);
        await service.Total.Should().BeEqualTo(300m);
        await service.IsAuthenticated.Should().BeTrue();
        await service.IsBusy.Should().BeFalse();
        await service.Error.Should().BeNull();
        await js.ReceivedCalls().Count().Should().BeEqualTo(0);
    }

    [Test]
    public async Task Initialize_RestoredSnapshotSkipsBackendAndPreservesItemsAndTotals()
    {
        var originalBackend = Substitute.For<ICartBackend>();
        originalBackend.GetAsync().Returns(Cart());
        var prerender = new CartService(Substitute.For<IJSRuntime>(), originalBackend);
        await prerender.InitializeAsync();
        var serialized = JsonSerializer.Serialize(prerender.State);
        var interactiveBackend = Substitute.For<ICartBackend>();
        var interactive = new CartService(Substitute.For<IJSRuntime>(), interactiveBackend)
        {
            State = JsonSerializer.Deserialize<CartState>(serialized)
        };
        await interactive.InitializeAsync();
        await interactiveBackend.DidNotReceive().GetAsync();
        await interactive.Items.Single().VariantId.Should().BeEqualTo(7);
        await interactive.ItemCount.Should().BeEqualTo(2);
        await interactive.Total.Should().BeEqualTo(300m);
        await interactive.IsAuthenticated.Should().BeTrue();
        await interactive.HasCart.Should().BeTrue();
    }

    [Test]
    public async Task Initialize_EmptyCartIsLoadedOnce()
    {
        var backend = Substitute.For<ICartBackend>();
        backend.GetAsync().Returns(new CartDto(false, []));
        var service = new CartService(Substitute.For<IJSRuntime>(), backend);
        await service.InitializeAsync();
        await service.InitializeAsync();
        await backend.Received(1).GetAsync();
        await service.HasCart.Should().BeTrue();
        await service.Items.Count.Should().BeEqualTo(0);
        await service.IsAuthenticated.Should().BeFalse();
    }

    [Test]
    public async Task Initialize_RestoredFailureWaitsForExplicitRetry()
    {
        var backend = Substitute.For<ICartBackend>();
        backend.GetAsync().Returns(Task.FromException<CartDto>(new HttpRequestException("Offline")));
        var prerender = new CartService(Substitute.For<IJSRuntime>(), backend);
        await prerender.InitializeAsync();
        await prerender.HasCart.Should().BeFalse();
        await prerender.Error.Should().BeEqualTo("Connection interrupted. Please try again.");
        var retryBackend = Substitute.For<ICartBackend>();
        retryBackend.GetAsync().Returns(Cart(3));
        var interactive = new CartService(Substitute.For<IJSRuntime>(), retryBackend)
        {
            State = JsonSerializer.Deserialize<CartState>(JsonSerializer.Serialize(prerender.State))
        };
        await interactive.InitializeAsync();
        await retryBackend.DidNotReceive().GetAsync();
        await interactive.RefreshAsync();
        await retryBackend.Received(1).GetAsync();
        await interactive.ItemCount.Should().BeEqualTo(3);
        await interactive.Error.Should().BeNull();
    }

    [Test]
    public async Task Refresh_ReplacesRestoredSnapshotWithCurrentServerCart()
    {
        var backend = Substitute.For<ICartBackend>();
        backend.GetAsync().Returns(new CartDto(false, []));
        var service = new CartService(Substitute.For<IJSRuntime>(), backend) { State = new(Cart()) };
        await service.RefreshAsync();
        await backend.Received(1).GetAsync();
        await service.Items.Count.Should().BeEqualTo(0);
        await service.Total.Should().BeEqualTo(0m);
        await service.IsAuthenticated.Should().BeFalse();
    }

    [Test]
    public async Task SetQuantity_PendingAndRejectedUpdatePreserveConfirmedCart()
    {
        var backend = Substitute.For<ICartBackend>();
        var pending = new TaskCompletionSource<CartDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.ChangeAsync(Arg.Any<CartLineRequest>(), false).Returns(pending.Task);
        var service = new CartService(Substitute.For<IJSRuntime>(), backend) { State = new(Cart()) };
        var update = service.SetQuantityAsync(7, 5);
        await service.IsBusy.Should().BeTrue();
        await service.ItemCount.Should().BeEqualTo(2);
        pending.SetException(new CommerceException("Insufficient stock."));
        await update;
        await service.Items.Single().Quantity.Should().BeEqualTo(2);
        await service.Total.Should().BeEqualTo(300m);
        await service.Error.Should().BeEqualTo("Insufficient stock.");
        await service.IsBusy.Should().BeFalse();
    }

    [Test]
    public async Task SetQuantity_UsesAuthoritativeResponseAndUpdatesBadge()
    {
        var backend = Substitute.For<ICartBackend>();
        backend.ChangeAsync(new CartLineRequest(7, 3), false).Returns(Cart(3));
        var js = Substitute.For<IJSRuntime>();
        var service = new CartService(js, backend) { State = new(Cart()) };
        await service.SetQuantityAsync(7, 3);
        await service.ItemCount.Should().BeEqualTo(3);
        await service.Total.Should().BeEqualTo(450m);
        var badgeCall = js.ReceivedCalls().Single();
        await badgeCall.GetArguments()[0]!.ToString().Should().BeEqualTo("window.jasUpdateCartBadge");
        await ((object?[])badgeCall.GetArguments()[1]!)[0].Should().BeEqualTo(3);
        await backend.DidNotReceive().GetAsync();
    }

    [Test]
    public async Task RemoveLastItem_InitializesEmptyCartWithoutRefetching()
    {
        var backend = Substitute.For<ICartBackend>();
        backend.ChangeAsync(new CartLineRequest(7, 0), false).Returns(new CartDto(true, []));
        var service = new CartService(Substitute.For<IJSRuntime>(), backend) { State = new(Cart()) };
        await service.SetQuantityAsync(7, 0);
        await service.InitializeAsync();
        await service.HasCart.Should().BeTrue();
        await service.ItemCount.Should().BeEqualTo(0);
        await service.Total.Should().BeEqualTo(0m);
        await backend.DidNotReceive().GetAsync();
    }

    [Test]
    public async Task AddThenCheckoutCompletion_UseServerResponsesAndRefreshAfterCheckout()
    {
        var backend = Substitute.For<ICartBackend>();
        backend.ChangeAsync(new CartLineRequest(7, 2), true).Returns(Cart());
        backend.GetAsync().Returns(new CartDto(true, []));
        var service = new CartService(Substitute.For<IJSRuntime>(), backend);
        await service.AddAsync(7, 2);
        await service.InitializeAsync();
        await backend.DidNotReceive().GetAsync();
        await service.ItemCount.Should().BeEqualTo(2);
        await service.CompleteCheckoutAsync(Guid.NewGuid());
        await backend.Received(1).GetAsync();
        await service.ItemCount.Should().BeEqualTo(0);
    }
}
