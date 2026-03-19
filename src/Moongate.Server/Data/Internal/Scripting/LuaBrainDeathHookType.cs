namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Identifies which death lifecycle hook should run for an NPC brain.
/// </summary>
public enum LuaBrainDeathHookType : byte
{
    BeforeDeath = 0,
    Death = 1,
    AfterDeath = 2
}
