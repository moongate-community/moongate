using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.Movement;

namespace Moongate.Server.Ultima.Data.Movement;

/// <summary>
///     The session values of movement.
/// </summary>
public static class MovementSessionKeys
{
    public static readonly SessionKey<MovementState?> State = new("Movement");
}
