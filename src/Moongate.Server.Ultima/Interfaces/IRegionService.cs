using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Finds the region of a place, as ModernUO: of the regions covering it, the highest priority, and on a tie the
///     deepest child.
/// </summary>
public interface IRegionService
{
    /// <summary>
    ///     Gets the region that applies at <paramref name="location" /> of <paramref name="map" />; null outside every
    ///     region.
    /// </summary>
    RegionContent? Find(MapType map, Point3D location);
}
