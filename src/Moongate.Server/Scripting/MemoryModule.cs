using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Scripting.Views;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Durable key/value memory for the current NPC brain, persisted across restarts. Self-implicit on the
/// mobile of the running tick; calling it outside a brain hook raises a Lua error. Values are scalars
/// (string, number, boolean).
/// </summary>
[ScriptModule("memory", "Durable per-NPC memory: remember facts across restarts.")]
public sealed class MemoryModule
{
    private readonly INpcMemoryService _memory;

    public MemoryModule(INpcMemoryService memory)
    {
        _memory = memory;
    }

    [ScriptFunction("all", "Returns a table of every stored key/value for the current NPC.")]
    public NpcMemoryLuaView All()
        => new(_memory.All());

    [ScriptFunction("delete", "Removes a key; true when it existed.")]
    public bool Delete(string key)
        => _memory.Delete(key);

    [ScriptFunction("get", "Returns the stored value for a key, or nil.")]
    public object? Get(string key)
        => _memory.Get(key)?.ToScalar();

    [ScriptFunction("set", "Stores a scalar (string/number/boolean) under a key; false when unsupported or rejected.")]
    public bool Set(string key, object value)
    {
        var stored = value switch
        {
            string text   => NpcMemoryValue.FromString(text),
            bool flag     => NpcMemoryValue.FromBoolean(flag),
            double number => NpcMemoryValue.FromNumber(number),
            long number   => NpcMemoryValue.FromNumber(number),
            int number    => NpcMemoryValue.FromNumber(number),
            _             => null
        };

        return stored is not null && _memory.Set(key, stored);
    }
}
