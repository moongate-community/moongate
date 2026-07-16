namespace Moongate.UO.Data.Types;

/// <summary>
/// Whether a skill may drift as it is used: the up/down/lock arrow the client shows next to each entry
/// in the skill list. Mirrors the stat locks but is a separate concept, as it is in ModernUO.
/// </summary>
public enum SkillLockType : byte
{
    Up = 0,
    Down = 1,
    Locked = 2
}
