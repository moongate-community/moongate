using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Metadata;

[PacketHandler(0xF3, PacketSizing.Variable)]
public sealed class VariableFixedBasePacket : BaseFixedPacket<VariableFixedBasePacket>, IOutgoingPacket
{
    public void Write(ref PacketWriter writer) { }
}
