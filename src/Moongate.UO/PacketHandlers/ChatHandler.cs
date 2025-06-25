using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Chat;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Handlers;
using Moongate.UO.Interfaces.Services.Systems;

namespace Moongate.UO.PacketHandlers;

public class ChatHandler : IGamePacketHandler
{
    private readonly INotificationSystem _notificationSystem;

    public ChatHandler(INotificationSystem notificationSystem)
    {
        _notificationSystem = notificationSystem;
    }

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is UnicodeSpeechRequestPacket speechRequest)
        {
            HandleChatMessage(session, speechRequest);
        }
    }

    private async Task HandleChatMessage(GameSession session, UnicodeSpeechRequestPacket speechRequest)
    {
        _notificationSystem.SendChatMessage(
            session.Mobile,
            speechRequest.MessageType,
            speechRequest.Hue,
            speechRequest.Text,
            -1,
            3 // Default font
        );
    }
}
