using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Told when a player enters the world in a region or walks into another one, such as to send it that region's
///     weather. Called on the game loop.
/// </summary>
public interface IRegionChangeListener
{
    /// <summary>
    ///     <paramref name="player" /> is now in <paramref name="current" />; <paramref name="previous" /> is null when it just
    ///     entered the world. Either is null outside every region.
    /// </summary>
    void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current);

    /// <summary>
    ///     <paramref name="player" /> left the world.
    /// </summary>
    void Left(Serial player);
}
