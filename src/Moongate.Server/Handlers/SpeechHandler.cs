using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.UnicodeSpeechPacket)]
/// <summary>
/// Represents SpeechHandler.
/// </summary>
public class SpeechHandler : BasePacketListener
{
    private readonly ISpeechService _speechService;

    public SpeechHandler(IOutgoingPacketQueue outgoingPacketQueue, ISpeechService speechService)
        : base(outgoingPacketQueue)
        => _speechService = speechService;

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not UnicodeSpeechPacket speechPacket)
        {
            return true;
        }

        var outgoingSpeechPacket = await _speechService.ProcessIncomingSpeechAsync(session, speechPacket);

        if (outgoingSpeechPacket is not null)
        {
            Enqueue(session, outgoingSpeechPacket);
        }

        return true;
    }
}
