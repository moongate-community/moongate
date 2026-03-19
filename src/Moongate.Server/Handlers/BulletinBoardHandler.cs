using Moongate.Network.Packets.Data.BulletinBoard;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.BulletinBoardMessagesPacket)]
public sealed class BulletinBoardHandler : BasePacketListener
{
    private readonly IBulletinBoardService _bulletinBoardService;

    public BulletinBoardHandler(IOutgoingPacketQueue outgoingPacketQueue, IBulletinBoardService bulletinBoardService)
        : base(outgoingPacketQueue)
    {
        _bulletinBoardService = bulletinBoardService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not BulletinBoardMessagesPacket boardPacket)
        {
            return false;
        }

        return boardPacket.Subcommand switch
        {
            BulletinBoardSubcommand.RequestMessage => await _bulletinBoardService.SendMessageAsync(
                                                          session,
                                                          boardPacket.BoardId,
                                                          boardPacket.MessageId
                                                      ),
            BulletinBoardSubcommand.RequestMessageSummary => await _bulletinBoardService.SendSummaryAsync(
                                                                 session,
                                                                 boardPacket.BoardId,
                                                                 boardPacket.MessageId
                                                             ),
            BulletinBoardSubcommand.PostMessage => await _bulletinBoardService.PostMessageAsync(session, boardPacket),
            BulletinBoardSubcommand.RemovePostedMessage => await _bulletinBoardService.RemoveMessageAsync(
                                                               session,
                                                               boardPacket.BoardId,
                                                               boardPacket.MessageId
                                                           ),
            _ => true
        };
    }
}
