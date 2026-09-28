using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Movement;

/// <summary>
///     Allows every step at the same height unless told otherwise, and records the steps it was asked about.
/// </summary>
public sealed class StubMovementService : IMovementService
{
    public bool Allow { get; set; } = true;

    public int LandingZ { get; set; }

    public bool ThrowMapNotLoaded { get; set; }

    public List<(MapType Map, Point3D From, DirectionType Direction)> Checks { get; } = [];

    public int GetAverageZ(MapType map, int x, int y)
    {
        return LandingZ;
    }

    public bool CheckMovement(
        MapType map,
        Point3D from,
        DirectionType direction,
        MovementAbilityType ability,
        out int newZ
    )
    {
        Checks.Add((map, from, direction));

        if (ThrowMapNotLoaded)
        {
            throw new KeyNotFoundException($"Map {map} was not loaded.");
        }

        newZ = Allow ? LandingZ : from.Z;

        return Allow;
    }
}
