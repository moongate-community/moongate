namespace Moongate.Server.Ultima.Data.Internal.Mobiles;

/// <summary>
///     When the next point of each bar of a mobile comes back, in milliseconds since 1970 (UTC); 0 for a bar that is
///     full, which waits a whole interval once it drops.
/// </summary>
public sealed class RegenerationClock
{
    public long HitsAt { get; set; }

    public long ManaAt { get; set; }

    public long StaminaAt { get; set; }
}
