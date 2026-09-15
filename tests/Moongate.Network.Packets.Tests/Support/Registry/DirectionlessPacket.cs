using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Registry;

[PacketHandler(0xD2, PacketSizing.Fixed, Length = 1)]
public sealed class DirectionlessPacket : IPacket
{
    public byte OpCode => 0xD2;
    public int Length => 1;
}
