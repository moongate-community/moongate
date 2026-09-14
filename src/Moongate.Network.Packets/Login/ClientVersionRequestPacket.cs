using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class ClientVersionRequestPacket : IOutgoingPacket
{
    private const byte PacketOpCode = 0xBD;
    private const int PacketLength = 3;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;

    public ClientVersionRequestPacket()
    {
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian(PacketLength);
    }
}
