namespace Moongate.Server.Ultima.Data.Internal.Mobiles;

/// <summary>
///     When a player loses its next point of hunger, in milliseconds since 1970 (UTC); 0 until the player is first seen
///     in the world.
/// </summary>
public sealed class HungerClock
{
    public long NextAt { get; set; }
}
