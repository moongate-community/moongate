namespace Moongate.Ultima.Types;

/// <summary>
///     Which way a skill may move, valued as the client sends and receives it.
/// </summary>
public enum SkillLockType : byte
{
    Up = 0,
    Down = 1,
    Locked = 2
}
