using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Incoming.Speech;

[PacketHandler(0x03, PacketSizing.Variable, Description = "Talk Request")]

/// <summary>
/// Represents TalkRequestPacket.
/// </summary>
public class TalkRequestPacket : BaseGameNetworkPacket
{
    public TalkRequestPacket()
        : base(0x03) { }

    protected override bool ParsePayload(ref SpanReader reader)
        => true;
}
