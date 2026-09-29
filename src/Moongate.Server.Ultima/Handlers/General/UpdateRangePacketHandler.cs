using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Answers the client's view range request (0xC8) with the server's (ultima.world.view_range), whatever it asked, as
///     ModernUO answers with its fixed 18: the server decides what the client sees.
/// </summary>
public sealed class UpdateRangePacketHandler : IPacketHandler<UpdateRangePacket>
{
    private readonly WorldConfig _world;
    private readonly IPacketSendService _sender;

    public UpdateRangePacketHandler(WorldConfig world, IPacketSendService sender)
    {
        _world = world;
        _sender = sender;
    }

    public void Handle(GameSession session, UpdateRangePacket packet)
    {
        _sender.TrySend(session.SessionId, new ViewRangePacket(_world.ViewRange));
    }
}
