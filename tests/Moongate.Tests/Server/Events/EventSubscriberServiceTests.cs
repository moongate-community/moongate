using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Services.Events;
using Moongate.Tests.Support;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Tests.Server.Events;

public class EventSubscriberServiceTests
{
    private sealed class SpySubscriber : IEventSubscriberRegistration
    {
        public IEventBus? SubscribedTo { get; private set; }

        public void Subscribe(IEventBus eventBus)
            => SubscribedTo = eventBus;
    }

    [Fact]
    public async Task StartAsync_WiresEverySubscriberToTheBus()
    {
        var bus = new StubEventBus();
        var first = new SpySubscriber();
        var second = new SpySubscriber();
        var service = new EventSubscriberService([first, second], bus);

        await service.StartAsync();

        Assert.Same(bus, first.SubscribedTo);
        Assert.Same(bus, second.SubscribedTo);
        Assert.Equal(2, service.Count);
    }

    [Fact]
    public async Task StartAsync_WithNoSubscribers_DoesNothing()
    {
        var service = new EventSubscriberService([], new StubEventBus());

        await service.StartAsync();

        Assert.Equal(0, service.Count);
    }
}
