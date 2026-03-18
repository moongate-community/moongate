namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Supported combat outcome hooks exposed to Lua brains.
/// </summary>
public enum LuaBrainCombatHookType : byte
{
    Attack = 0,
    MissedAttack = 1,
    Attacked = 2,
    MissedByAttack = 3
}
