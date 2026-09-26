using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Metadata;

[PacketHandler(0xF2, (PacketSizing)99)]
public sealed class UnknownSizingPacket : BasePacket<UnknownSizingPacket>, IOutgoingPacket
{
    public override int Length => 3;

    public void Write(ref PacketWriter writer)
    {
    }
}
