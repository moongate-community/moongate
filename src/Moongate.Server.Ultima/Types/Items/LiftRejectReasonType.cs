namespace Moongate.Server.Ultima.Types.Items;

/// <summary>
///     Why the server refused to let the player pick an item up (0x27), as the client numbers the messages.
/// </summary>
public enum LiftRejectReasonType : byte
{
    CannotLift = 0,
    OutOfRange = 1,
    OutOfSight = 2,
    TryToSteal = 3,
    AreHolding = 4,
    Inspecific = 5
}
