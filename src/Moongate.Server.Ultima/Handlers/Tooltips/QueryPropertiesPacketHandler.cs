using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Server.Ultima.Handlers.Tooltips;

/// <summary>
///     Answers the client's request for tooltips (0xD6) with one 0xD6 per object its character can see; the others
///     get nothing, as ModernUO.
/// </summary>
public sealed class QueryPropertiesPacketHandler : IPacketHandler<QueryPropertiesPacket>
{
    private readonly ITooltipService _tooltips;
    private readonly IPacketSendService _sender;

    public QueryPropertiesPacketHandler(ITooltipService tooltips, IPacketSendService sender)
    {
        _tooltips = tooltips;
        _sender = sender;
    }

    public void Handle(GameSession session, QueryPropertiesPacket packet)
    {
        foreach (var serial in packet.Serials)
        {
            if (_tooltips.TryBuildFor(session.CharacterId, serial, out var list, session.AccountType))
            {
                _sender.TrySend(session.SessionId, new PropertyListPacket(serial, list));
            }
        }
    }
}
