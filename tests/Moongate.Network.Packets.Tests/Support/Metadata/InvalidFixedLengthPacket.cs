using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Metadata;

[PacketHandler(0xF0, PacketSizing.Fixed, Length = 0)]
public sealed class InvalidFixedLengthPacket : BaseFixedPacket<InvalidFixedLengthPacket>, IOutgoingPacket
{
    public void Write(ref PacketWriter writer) { }
}
