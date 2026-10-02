using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Movement;

/// <summary>
///     A flat world where a step is allowed unless its cell is in <see cref="Walls" />, with a diagonal step also needing
///     both cells beside it, as the real movement; <see cref="Heights" /> gives a cell a landing height of its own, and
///     a step may not climb more than <see cref="MaxClimb" />. It counts the checks it is asked.
/// </summary>
public sealed class GridMovementService : IMovementService
{
    public HashSet<(int X, int Y)> Walls { get; } = [];

    public Dictionary<(int X, int Y), int> Heights { get; } = [];

    /// <summary>
    ///     Gets the cells with a floor above their ground, such as a balcony: a step lands on it when it comes from
    ///     within <see cref="MaxClimb" /> of its height, and on the ground otherwise.
    /// </summary>
    public Dictionary<(int X, int Y), int> Floors { get; } = [];

    public int MaxClimb { get; set; } = 2;

    public bool ThrowMapNotLoaded { get; set; }

    public int Checks { get; private set; }

    /// <summary>
    ///     Walls every cell of a rectangle given by two corners, both included.
    /// </summary>
    public GridMovementService Wall(int x1, int y1, int x2, int y2)
    {
        for (var x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
        {
            for (var y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
            {
                Walls.Add((x, y));
            }
        }

        return this;
    }

    public int GetAverageZ(MapType map, int x, int y)
    {
        return Heights.GetValueOrDefault((x, y));
    }

    public bool TryGetDropZ(MapType map, int x, int y, int maxZ, out int z)
    {
        z = Heights.GetValueOrDefault((x, y));

        return true;
    }

    public bool TryGetSpawnZ(MapType map, int x, int y, int maxZ, out int z)
    {
        z = Heights.GetValueOrDefault((x, y));

        return !Walls.Contains((x, y));
    }

    public bool TryGetSwimZ(MapType map, int x, int y, out int z)
    {
        z = 0;

        return false;
    }

    public bool CheckMovement(
        MapType map,
        Point3D from,
        DirectionType direction,
        MovementAbilityType ability,
        out int newZ
    )
    {
        Checks++;

        if (ThrowMapNotLoaded)
        {
            throw new KeyNotFoundException($"Map {map} was not loaded.");
        }

        newZ = from.Z;
        var facing = (DirectionType)((byte)direction & 0x7);
        var forward = from.Move(facing);

        if (!Open(forward, from.Z))
        {
            return false;
        }

        if (((byte)facing & 0x1) == 0x1)
        {
            var left = from.Move((DirectionType)(((byte)facing - 1) & 0x7));
            var right = from.Move((DirectionType)(((byte)facing + 1) & 0x7));

            if (!Open(left, from.Z) || !Open(right, from.Z))
            {
                return false;
            }
        }

        newZ = Landing(forward, from.Z)!.Value;

        return true;
    }

    private bool Open(Point3D cell, int fromZ)
    {
        return cell.X >= 0 && cell.Y >= 0 && !Walls.Contains((cell.X, cell.Y)) && Landing(cell, fromZ) is not null;
    }

    // Where a step from a height lands on a cell: its upper floor when level with it, else its ground when not too
    // high; null when neither can be stepped onto.
    private int? Landing(Point3D cell, int fromZ)
    {
        if (Floors.TryGetValue((cell.X, cell.Y), out var floor) && Math.Abs(floor - fromZ) <= MaxClimb)
        {
            return floor;
        }

        var ground = Heights.GetValueOrDefault((cell.X, cell.Y));

        return ground - fromZ <= MaxClimb ? ground : null;
    }
}
