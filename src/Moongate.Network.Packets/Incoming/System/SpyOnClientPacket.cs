using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Incoming.System;

[PacketHandler(0xD9, PacketSizing.Variable, Description = "Spy On Client")]
/// <summary>
/// Represents SpyOnClientPacket.
/// </summary>
public class SpyOnClientPacket : BaseGameNetworkPacket
{
    public SpyOnClientPacket()
        : base(0xD9) { }

    protected override bool ParsePayload(ref SpanReader reader)
        => true;
}
