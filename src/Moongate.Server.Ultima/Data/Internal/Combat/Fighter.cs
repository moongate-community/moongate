using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Internal.Combat;

/// <summary>
///     A mobile that fights: whom, when it swings next and when it gives the fight up. Kept by the combat service, not
///     saved.
/// </summary>
internal sealed class Fighter
{
    public required MobileEntity Attacker { get; init; }

    public required MobileEntity Target { get; set; }

    public DateTimeOffset NextSwingAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
