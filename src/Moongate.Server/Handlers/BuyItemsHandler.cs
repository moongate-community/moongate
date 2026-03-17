using Moongate.Network.Packets.Incoming.Trading;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(0x3B)]
public sealed class BuyItemsHandler : BasePacketListener
{
    private readonly IPlayerSellBuyService _playerSellBuyService;

    public BuyItemsHandler(IOutgoingPacketQueue outgoingPacketQueue, IPlayerSellBuyService playerSellBuyService)
        : base(outgoingPacketQueue)
    {
        _playerSellBuyService = playerSellBuyService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not BuyItemsPacket buyItemsPacket)
        {
            return false;
        }

        await _playerSellBuyService.HandleBuyItemsAsync(session.SessionId, buyItemsPacket);

        return true;
    }
}
