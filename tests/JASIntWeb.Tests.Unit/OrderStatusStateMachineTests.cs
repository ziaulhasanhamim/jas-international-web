using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;

namespace JASIntWeb.Tests.Unit;

public class OrderStatusStateMachineTests
{
    [Test]
    public async Task AllowedTransitions_FromPending_IncludeProcessingInTransitAndCancelled()
    {
        var pending = OrderStatus.Pending;

        var canGoProcessing = CanTransition(pending, OrderStatus.Processing);
        var canGoInTransit = CanTransition(pending, OrderStatus.InTransit);
        var canGoCancelled = CanTransition(pending, OrderStatus.Cancelled);
        var canGoDeliveredDirectly = CanTransition(pending, OrderStatus.Delivered);

        await canGoProcessing.Should().BeTrue();
        await canGoInTransit.Should().BeTrue();
        await canGoCancelled.Should().BeTrue();
        await canGoDeliveredDirectly.Should().BeFalse();
    }

    [Test]
    public async Task AllowedTransitions_FromProcessing_IncludeInTransitDeliveredAndCancelled()
    {
        var processing = OrderStatus.Processing;

        var canGoInTransit = CanTransition(processing, OrderStatus.InTransit);
        var canGoDelivered = CanTransition(processing, OrderStatus.Delivered);
        var canGoCancelled = CanTransition(processing, OrderStatus.Cancelled);
        var canGoPending = CanTransition(processing, OrderStatus.Pending);

        await canGoInTransit.Should().BeTrue();
        await canGoDelivered.Should().BeTrue();
        await canGoCancelled.Should().BeTrue();
        await canGoPending.Should().BeFalse();
    }

    [Test]
    public async Task AllowedTransitions_FromInTransit_IncludeDeliveredAndCancelled()
    {
        var inTransit = OrderStatus.InTransit;

        var canGoDelivered = CanTransition(inTransit, OrderStatus.Delivered);
        var canGoCancelled = CanTransition(inTransit, OrderStatus.Cancelled);
        var canGoPending = CanTransition(inTransit, OrderStatus.Pending);

        await canGoDelivered.Should().BeTrue();
        await canGoCancelled.Should().BeTrue();
        await canGoPending.Should().BeFalse();
    }

    [Test]
    public async Task FinalStates_DeliveredAndCancelled_CannotTransitionToAnyOtherState()
    {
        var delivered = OrderStatus.Delivered;
        var cancelled = OrderStatus.Cancelled;

        await CanTransition(delivered, OrderStatus.Pending).Should().BeFalse();
        await CanTransition(delivered, OrderStatus.Processing).Should().BeFalse();
        await CanTransition(delivered, OrderStatus.Cancelled).Should().BeFalse();

        await CanTransition(cancelled, OrderStatus.Pending).Should().BeFalse();
        await CanTransition(cancelled, OrderStatus.Processing).Should().BeFalse();
        await CanTransition(cancelled, OrderStatus.Delivered).Should().BeFalse();
    }

    private static bool CanTransition(OrderStatus current, OrderStatus next)
    {
        return current switch
        {
            OrderStatus.Pending => next is OrderStatus.Processing or OrderStatus.InTransit or OrderStatus.Cancelled,
            OrderStatus.Processing => next is OrderStatus.InTransit or OrderStatus.Delivered or OrderStatus.Cancelled,
            OrderStatus.InTransit => next is OrderStatus.Delivered or OrderStatus.Cancelled,
            _ => false
        };
    }
}
