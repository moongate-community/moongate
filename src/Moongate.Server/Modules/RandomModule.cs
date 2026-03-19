using Moongate.Scripting.Attributes.Scripts;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

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

    [ScriptFunction("element", "Returns a random element from an array-like Lua table (1..n), or nil if empty.")]
    public DynValue Element(Table? values)
    {
        if (values is null)
        {
            return DynValue.Nil;
        }

        var count = values.Length;

        if (count <= 0)
        {
            return DynValue.Nil;
        }

        var index = Random.Shared.Next(1, count + 1);

        return values.Get(index);
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
