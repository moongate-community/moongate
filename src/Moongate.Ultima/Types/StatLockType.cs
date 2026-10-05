namespace Moongate.Ultima.Types;

/// <summary>
///     Which way a strength, dexterity or intelligence may move, valued as the client sends and receives it.
/// </summary>
public enum StatLockType : byte
{
    Up = 0,
    Down = 1,
    Locked = 2
}
