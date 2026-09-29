using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.Targeting;

namespace Moongate.Server.Ultima.Data.Targeting;

/// <summary>
///     The session values of the target cursor.
/// </summary>
public static class TargetSessionKeys
{
    public static readonly SessionKey<TargetState?> State = new("TargetState");
}
