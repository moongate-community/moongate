using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.ChatTextPacket), RegisterPacketHandler(PacketDefinition.OpenChatWindowPacket)]
public sealed class ChatHandler : BasePacketListener
{
    private readonly IChatSystemService _chatSystemService;

    public ChatHandler(IOutgoingPacketQueue outgoingPacketQueue, IChatSystemService chatSystemService)
        : base(outgoingPacketQueue)
    {
        _chatSystemService = chatSystemService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is OpenChatWindowPacket openChatWindowPacket)
        {
            await _chatSystemService.OpenWindowAsync(session);

            return true;
        }

        if (packet is ChatTextPacket chatTextPacket)
        {
            await _chatSystemService.HandleChatActionAsync(session, chatTextPacket);
        }

        return true;
    }
}
