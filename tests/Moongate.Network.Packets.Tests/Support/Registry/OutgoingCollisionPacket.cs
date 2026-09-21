using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Registry;

[PacketHandler(0xE1, PacketSizing.Fixed, Length = 1)]
public sealed class OutgoingCollisionPacket : BaseFixedPacket<OutgoingCollisionPacket>, IOutgoingPacket
{
    public void Write(ref PacketWriter writer)
        => writer.WriteByte(OpCode);
}
