namespace Moongate.Server.Ultima.Types.Targeting;

/// <summary>
///     Why a target ended without a pick: the player or the server cancelled it, a newer target replaced it, or the
///     session closed.
/// </summary>
public enum TargetCancelType : byte
{
    Canceled,
    Overridden,
    Disconnected
}
