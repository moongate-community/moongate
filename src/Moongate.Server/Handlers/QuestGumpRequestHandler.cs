using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.QuestGumpRequestPacket)]
public sealed class QuestGumpRequestHandler : BasePacketListener
{
    private readonly IGameEventBusService _gameEventBusService;

    public QuestGumpRequestHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameEventBusService gameEventBusService
    )
        : base(outgoingPacketQueue)
    {
        _gameEventBusService = gameEventBusService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not QuestGumpRequestPacket questGumpRequestPacket)
        {
            return true;
        }

        if (session.CharacterId == 0 ||
            session.Character is null ||
            !session.Character.IsPlayer ||
            session.CharacterId != questGumpRequestPacket.PlayerSerial ||
            questGumpRequestPacket.EncodedCommandId != 0x0032 ||
            questGumpRequestPacket.EncodedCommandData.Length != 1 ||
            questGumpRequestPacket.EncodedCommandData.Span[0] != 0x07)
        {
            return true;
        }

        await _gameEventBusService.PublishAsync(new QuestJournalRequestedEvent(session.SessionId));

        return true;
    }
}
