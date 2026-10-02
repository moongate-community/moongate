using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class RegionEventPublisherTests
{
    [Fact]
    public async Task RegionChanged_PublishesTheChangeOnTheEventBus()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var events = container.Resolve<IMoongateEventBus>();
        var published = new TaskCompletionSource<PlayerRegionChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.Subscribe<PlayerRegionChangedEvent>(
            (change, _) =>
            {
                published.TrySetResult(change);

                return Task.CompletedTask;
            }
        );
        var aria = new MobileEntity { Id = new Serial(2), Name = "Aria", Map = MapType.Trammel };
        var britain = new RegionContent { Map = MapType.Trammel, Name = "Britain" };

        new RegionEventPublisher(events).RegionChanged(aria, null, britain);

        var change = await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(aria, change.Player);
        Assert.Null(change.Previous);
        Assert.Same(britain, change.Current);
    }

    [Fact]
    public async Task RegionChanged_AListenerThatFails_DoesNotFailTheStep()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var events = container.Resolve<IMoongateEventBus>();
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.Subscribe<PlayerRegionChangedEvent>(
            (_, _) =>
            {
                called.TrySetResult();

                throw new InvalidOperationException("a broken listener");
            }
        );

        new RegionEventPublisher(events).RegionChanged(new MobileEntity { Id = new Serial(2), Name = "Aria" }, null, null);

        await called.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
