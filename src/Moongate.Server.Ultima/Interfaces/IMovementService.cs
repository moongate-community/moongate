using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Decides whether a mover can take one step and at which height it lands, from the map terrain, the statics and
///     the tile flags, with the same rules as ModernUO and the client.
/// </summary>
/// <remarks>
///     A mover is 16 units tall and can climb 2 units in one step; a bridge, such as a stair, counts half its height.
///     World items, mobiles and placed multis are not considered yet. Call it from the game loop, as
///     <see cref="IMapService" />.
/// </remarks>
public interface IMovementService
{
    /// <summary>
    ///     Gets the height of the terrain at the centre of cell <paramref name="x" />, <paramref name="y" />, from the
    ///     heights of its four corners; a corner outside the map counts as 0.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     <paramref name="map" /> was not loaded.
    /// </exception>
    int GetAverageZ(MapType map, int x, int y);

    /// <summary>
    ///     Checks one step from <paramref name="from" /> towards <paramref name="direction" />; only its low three bits count, so the running flag is
    ///     ignored. A diagonal step also needs both cells beside it to be passable.
    /// </summary>
    /// <param name="newZ">
    ///     The height the mover lands at, or its starting height when the step is not allowed.
    /// </param>
    /// <returns>
    ///     True when the step is allowed; false when it is blocked or leaves the map.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    ///     <paramref name="map" /> was not loaded.
    /// </exception>
    bool CheckMovement(
        MapType map,
        Point3D from,
        DirectionType direction,
        MovementAbilityType ability,
        out int newZ
    );
}
