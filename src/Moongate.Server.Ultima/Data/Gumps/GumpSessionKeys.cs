using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.Gumps;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     The session values of the gumps.
/// </summary>
public static class GumpSessionKeys
{
    public static readonly SessionKey<GumpState?> State = new("GumpState");
}
