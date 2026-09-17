using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class MutableOutgoingPacket : IOutgoingPacket
{
    public byte OpCode => 0x73;
    public int Length => 2;
    public byte Sequence { get; set; }
    public int WriteCount { get; private set; }

    public void Write(ref PacketWriter writer)
    {
        WriteCount++;
        writer.WriteByte(OpCode);
        writer.WriteByte(Sequence);
    }
}
