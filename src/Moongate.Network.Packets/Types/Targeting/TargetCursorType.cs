namespace Moongate.Network.Packets.Types.Targeting;

/// <summary>
/// Defines cursor behavior for the target request.
/// </summary>
public enum TargetCursorType : byte
{
    Neutral = 0,
    Harmful = 1,
    Helpful = 2,
    CancelCurrentTargeting = 3
}
