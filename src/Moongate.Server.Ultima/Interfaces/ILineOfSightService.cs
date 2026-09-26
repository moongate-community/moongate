using Moongate.Core.Geometry;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Decides whether one point sees another from the map terrain and statics: POL's integer 3D line walk with
///     ModernUO's rules.
/// </summary>
/// <remarks>
///     Pass both points at eye or target height; a mobile's eye is its Z plus 14. Statics flagged <c>Window</c> or
///     <c>NoShoot</c> and terrain block the line; a blocker at the target's cell and height does not. Points farther
///     than <c>line_of_sight.max_distance</c> along X or Y are never in sight. World items, mobiles and placed multis
///     are not considered yet. Call it from the game loop, as <see cref="IMapService" />.
/// </remarks>
public interface ILineOfSightService
{
    /// <summary>
    ///     Gets whether <paramref name="origin" /> sees <paramref name="target" /> on <paramref name="map" />; false when either
    ///     point is outside the map or too far.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     <paramref name="map" /> was not loaded.
    /// </exception>
    bool HasLineOfSight(MapType map, Point3D origin, Point3D target);
}
