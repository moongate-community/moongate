using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Events;

namespace Moongate.Tests.Server.Services.Events;

public sealed class EventBusServiceTests
{
    [Fact]
    public async Task PublishAsync_OnEventSubscriber_ReceivesSameMessage()
    {
        using var container = new Container();
        MoongateStartedEvent? received = null;
        container.RegisterMoongateEventBus()
            .RegisterMoongateService<IEventBusService, EventBusService>();
        container.OnEvent<MoongateStartedEvent>((message, _) =>
        {
            received = message;
            return Task.CompletedTask;
        });
        var message = new MoongateStartedEvent();

        await container.Resolve<IEventBusService>().PublishAsync(message);

        Assert.Same(message, received);
    }

    [Fact]
    public async Task Subscribe_RawBusPublicationAndDisposedToken_ForwardsSharedBusBehavior()
    {
        using var container = new Container();
        var received = 0;
        container.RegisterMoongateEventBus()
            .RegisterMoongateService<IEventBusService, EventBusService>();
        var service = container.Resolve<IEventBusService>();
        var subscription = service.Subscribe<MoongateStoppingEvent>((_, _) =>
        {
            received++;
            return Task.CompletedTask;
        });
        var eventBus = container.Resolve<IMoongateEventBus>();

        await eventBus.PublishAsync(new MoongateStoppingEvent());
        subscription.Dispose();
        await eventBus.PublishAsync(new MoongateStoppingEvent());

        Assert.Equal(1, received);
    }

    [Fact]
    public async Task Resolve_MultipleConsumers_ReturnsSingletonAndForwardsCancellationToken()
    {
        using var container = new Container();
        using var cancellationSource = new CancellationTokenSource();
        CancellationToken receivedToken = default;
        container.RegisterMoongateEventBus()
            .RegisterMoongateService<IEventBusService, EventBusService>();
        var first = container.Resolve<IEventBusService>();
        var second = container.Resolve<IEventBusService>();
        first.Subscribe<MoongateStoppedEvent>((_, cancellationToken) =>
        {
            receivedToken = cancellationToken;
            return Task.CompletedTask;
        });

        await second.PublishAsync(new MoongateStoppedEvent(), cancellationSource.Token);

        Assert.Same(first, second);
        Assert.Equal(cancellationSource.Token, receivedToken);
    }
}
