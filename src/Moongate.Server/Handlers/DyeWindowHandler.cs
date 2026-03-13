using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.DyeWindowPacket)]
public sealed class DyeWindowHandler : BasePacketListener
{
    private readonly IDyeColorService _dyeColorService;

    public DyeWindowHandler(IOutgoingPacketQueue outgoingPacketQueue, IDyeColorService dyeColorService)
        : base(outgoingPacketQueue)
    {
        _dyeColorService = dyeColorService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not DyeWindowPacket dyeWindowPacket)
        {
            return false;
        }

        return await _dyeColorService.HandleResponseAsync(session, dyeWindowPacket);
    }
}
