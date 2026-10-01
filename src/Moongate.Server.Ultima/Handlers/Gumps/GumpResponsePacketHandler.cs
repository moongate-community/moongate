using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Server.Ultima.Handlers.Gumps;

/// <summary>
///     Hands the client's answer to a gump (0xB1) to <see cref="IGumpService" />, which checks it.
/// </summary>
public sealed class GumpResponsePacketHandler : IPacketHandler<GumpResponsePacket>
{
    private readonly IGumpService _gumps;

    public GumpResponsePacketHandler(IGumpService gumps)
    {
        _gumps = gumps;
    }

    public void Handle(GameSession session, GumpResponsePacket packet)
    {
        _gumps.Respond(session, packet);
    }
}
