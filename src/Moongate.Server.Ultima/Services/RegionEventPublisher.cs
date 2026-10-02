using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Tells the event bus, and so the scripts and plugins that listen, when a player changes region: it publishes
///     <see cref="PlayerRegionChangedEvent" />. The light, the weather and the like listen to the regions directly.
/// </summary>
public sealed class RegionEventPublisher : IRegionChangeListener
{
    private readonly ILogger _logger = Log.ForContext<RegionEventPublisher>();
    private readonly IMoongateEventBus _events;

    public RegionEventPublisher(IMoongateEventBus events)
    {
        _events = events;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        // Not awaited: the step that changed the region goes on; a listener that fails is logged.
        _ = _events.PublishAsync(new PlayerRegionChangedEvent(player, previous, current), CancellationToken.None)
                   .ContinueWith(
                       task => _logger.Warning(task.Exception, "Publishing the region change of {Player} failed", player.Name),
                       CancellationToken.None,
                       TaskContinuationOptions.OnlyOnFaulted,
                       TaskScheduler.Default
                   );
    }

    public void Left(Serial player)
    {
    }
}
