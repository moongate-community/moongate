using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published when a player's character walks, or is teleported, from a region into another, or changes map; either
///     region is null outside every region, and <paramref name="Previous" /> also when the character just entered the
///     world.
/// </summary>
public sealed record PlayerRegionChangedEvent(MobileEntity Player, RegionContent? Previous, RegionContent? Current)
    : IMoongateEvent;
