using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Chat;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Handlers;

namespace Moongate.UO.PacketHandlers;

public class ChatHandler  : IGamePacketHandler
{
    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is UnicodeSpeechRequestPacket speechRequest)
        {
            HandleChatMessage(session, speechRequest);
        }
    }

    private async Task HandleChatMessage(GameSession session, UnicodeSpeechRequestPacket speechRequest)
    {
        session.Mobile.Speech(
            speechRequest.MessageType,
            speechRequest.Hue,
            speechRequest.Text
        );
    }
}
