using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     Finds where someone can appear beside a spot, as a guard beside its criminal, instead of on it.
/// </summary>
internal static class SpotBeside
{
    private static readonly DirectionType[] Directions =
    [
        DirectionType.North, DirectionType.NorthEast, DirectionType.East, DirectionType.SouthEast,
        DirectionType.South, DirectionType.SouthWest, DirectionType.West, DirectionType.NorthWest
    ];

    /// <summary>
    ///     Gets a tile one step from <paramref name="center" /> that a walking mobile standing there could step onto,
    ///     so not one behind a wall, and that nobody stands on; the first found going round from a direction picked at
    ///     random. Null when there is none, or the map is not loaded.
    /// </summary>
    public static Point3D? Find(IMovementService movement, ISectorService sectors, MapType map, Point3D center, Random? random = null)
    {
        var first = (random ?? Random.Shared).Next(Directions.Length);

        for (var turn = 0; turn < Directions.Length; turn++)
        {
            var direction = Directions[(first + turn) % Directions.Length];
            var (x, y) = Offset(direction);
            int z;

            try
            {
                if (!movement.CheckMovement(map, center, direction, MovementAbilityType.Walk, out z))
                {
                    continue;
                }
            }
            catch (KeyNotFoundException)
            {
                return null;
            }

            var spot = new Point3D(center.X + x, center.Y + y, z);

            if (sectors.GetMobilesInRange(map, spot, 0).Count == 0)
            {
                return spot;
            }
        }

        return null;
    }

    private static (int X, int Y) Offset(DirectionType direction)
    {
        return direction switch
        {
            DirectionType.North     => (0, -1),
            DirectionType.NorthEast => (1, -1),
            DirectionType.East      => (1, 0),
            DirectionType.SouthEast => (1, 1),
            DirectionType.South     => (0, 1),
            DirectionType.SouthWest => (-1, 1),
            DirectionType.West      => (-1, 0),
            _                       => (-1, -1)
        };
    }
}
