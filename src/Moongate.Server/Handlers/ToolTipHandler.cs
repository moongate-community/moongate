using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.System;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.MegaClilocPacket)]
public class ToolTipHandler : BasePacketListener
{
    public ToolTipHandler(IOutgoingPacketQueue outgoingPacketQueue)
        : base(outgoingPacketQueue)
    { }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is MegaClilocPacket clilocPacket)
        {
            return await HandleMegaClilocPacketAsync(session, clilocPacket);
        }

        return true;
    }

    private Task<bool> HandleMegaClilocPacketAsync(GameSession session, MegaClilocPacket clilocPacket)
        => Task.FromResult(true);
}
