using Moongate.Core.Geometry;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Answers how dark it is at a spot: the region's own level when it has one, otherwise the time of
/// day. Higher is darker.
/// </summary>
public interface ILightService
{
    /// <summary>
    /// A fixed level that wins over everything, for a GM to force day or night. Null follows the
    /// normal rules.
    /// </summary>
    int? Override { get; set; }

    /// <summary>The light level at <paramref name="position" /> on <paramref name="mapId" />.</summary>
    int LevelFor(int mapId, Point3D position);
}
