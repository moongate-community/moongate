namespace Moongate.Server.Ultima.Types.Targeting;

/// <summary>
///     What the target cursor may pick (0x6C): an object only, or any location as well.
/// </summary>
public enum TargetCursorType : byte
{
    Object = 0,
    Location = 1
}
