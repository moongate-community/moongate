using Moongate.Core.Types;

namespace Moongate.Persistence.Entities;

public sealed record NpcMemoryValue(MemoryValueType Type, string? Text, double Number, bool Flag)
{
    public static NpcMemoryValue FromString(string text) => new(MemoryValueType.String, text, 0, false);

    public static NpcMemoryValue FromNumber(double number) => new(MemoryValueType.Number, null, number, false);

    public static NpcMemoryValue FromBoolean(bool flag) => new(MemoryValueType.Boolean, null, 0, flag);

    public object? ToScalar()
        => Type switch
        {
            MemoryValueType.String => Text,
            MemoryValueType.Number => Number,
            MemoryValueType.Boolean => Flag,
            _ => null
        };
}
