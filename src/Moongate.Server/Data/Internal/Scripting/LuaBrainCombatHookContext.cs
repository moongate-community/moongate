using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Deferred combat outcome notification for a Lua brain.
/// </summary>
public readonly record struct LuaBrainCombatHookContext(
    LuaBrainCombatHookType HookType,
    Serial OtherMobileId,
    Dictionary<string, object?> Payload
);
