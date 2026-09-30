using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Finds the region of a place, as ModernUO: of the regions covering it, the highest priority, and on a tie the
///     deepest child. It also keeps the region each player in the world stands in, told by the mobile service.
/// </summary>
public interface IRegionService
{
    /// <summary>
    ///     Gets the region that applies at <paramref name="location" /> of <paramref name="map" />; null outside every
    ///     region.
    /// </summary>
    RegionContent? Find(MapType map, Point3D location);

    /// <summary>
    ///     Gets the region the player stands in; null outside every region or for a mobile that is not a player in the
    ///     world.
    /// </summary>
    RegionContent? Current(Serial mobile);

    /// <summary>
    ///     Starts following a player entering the world; NPCs are ignored.
    /// </summary>
    void Entered(MobileEntity mobile);

    /// <summary>
    ///     Updates the region of a player that moved; a change is logged at debug.
    /// </summary>
    void Moved(MobileEntity mobile);

    /// <summary>
    ///     Stops following a mobile leaving the world.
    /// </summary>
    void Left(Serial mobile);
}
