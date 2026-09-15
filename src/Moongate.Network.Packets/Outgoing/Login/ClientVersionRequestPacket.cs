using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Outgoing.Login;

[PacketHandler(0xBD, PacketSizing.Fixed, Length = 3)]
public sealed class ClientVersionRequestPacket : BaseFixedPacket<ClientVersionRequestPacket>, IOutgoingPacket
{
    public ClientVersionRequestPacket()
    {
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
    }
}
