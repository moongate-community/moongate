using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Pending death notification consumed by Lua brain hooks.
/// </summary>
public readonly record struct LuaBrainDeathContext(
    LuaBrainDeathHookType HookType,
    Serial? ByCharacterId,
    Dictionary<string, object?> Context
);
