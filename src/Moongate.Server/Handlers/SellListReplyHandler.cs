using Moongate.Network.Packets.Incoming.Trading;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(0x9F)]
public sealed class SellListReplyHandler : BasePacketListener
{
    private readonly IPlayerSellBuyService _playerSellBuyService;

    public SellListReplyHandler(IOutgoingPacketQueue outgoingPacketQueue, IPlayerSellBuyService playerSellBuyService)
        : base(outgoingPacketQueue)
    {
        _playerSellBuyService = playerSellBuyService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not SellListReplyPacket sellListReplyPacket)
        {
            return false;
        }

        await _playerSellBuyService.HandleSellListReplyAsync(session.SessionId, sellListReplyPacket);

        return true;
    }
}
