using Moongate.Scripting.Attributes.Scripts;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Modules;

[ScriptModule("random", "Provides random helpers for scripts.")]

/// <summary>
/// Exposes random utility helpers to Lua scripts.
/// </summary>
public sealed class RandomModule
{
    private static readonly DirectionType[] Directions =
    [
        DirectionType.North,
        DirectionType.NorthEast,
        DirectionType.East,
        DirectionType.SouthEast,
        DirectionType.South,
        DirectionType.SouthWest,
        DirectionType.West,
        DirectionType.NorthWest
    ];

    [ScriptFunction("direction", "Returns a random base direction.")]
    public DirectionType Direction()
    {
        var index = Random.Shared.Next(Directions.Length);

        return Directions[index];
    }

    [ScriptFunction("int", "Returns a random integer in the inclusive range [min, max].")]
    public int Int(int min, int max)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }

        return Random.Shared.Next(min, max + 1);
    }
}
