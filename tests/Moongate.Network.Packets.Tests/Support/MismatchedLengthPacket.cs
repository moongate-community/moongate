using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Support;

internal sealed class MismatchedLengthPacket : IOutgoingPacket
{
    public byte OpCode => 0x01;
    public int Length => 2;

    public void Write(ref PacketWriter writer)
    {
        writer.WriteByte(OpCode);
    }
}
