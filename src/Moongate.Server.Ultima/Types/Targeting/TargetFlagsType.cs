namespace Moongate.Server.Ultima.Types.Targeting;

/// <summary>
///     How the client draws the target cursor (0x6C); <see cref="Cancel" /> takes the cursor away.
/// </summary>
public enum TargetFlagsType : byte
{
    Neutral = 0,
    Harmful = 1,
    Beneficial = 2,
    Cancel = 3
}
