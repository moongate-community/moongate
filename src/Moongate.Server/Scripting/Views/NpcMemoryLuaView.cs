using Moongate.Persistence.Entities;
using SquidStd.Scripting.Lua.Interfaces.Scripts;

namespace Moongate.Server.Scripting.Views;

/// <summary>Projects a mobile's stored memory into a Lua table of key → scalar value.</summary>
public sealed record NpcMemoryLuaView(IReadOnlyDictionary<string, NpcMemoryValue> Memory) : ILuaTable
{
    public Dictionary<string, object?> ToDictionary()
        => Memory.ToDictionary(pair => pair.Key, pair => pair.Value.ToScalar());
}
