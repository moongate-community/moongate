using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Metadata;

[PacketHandler(0xF1, PacketSizing.Variable, Length = 4, MinimumLength = 4)]
public sealed class InvalidVariableLengthPacket : BasePacket<InvalidVariableLengthPacket>, IOutgoingPacket
{
    public override int Length => 4;

    public void Write(ref PacketWriter writer) { }
}
