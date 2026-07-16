using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class DeleteCharacterPacketTests
{
    [Fact]
    public void Read_ParsesSlot()
    {
        var buffer = new byte[39];
        buffer[0] = 0x83;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(31), 2); // slot, after the 30-byte password

        var reader = new SpanReader(buffer);

        Assert.Equal(2, DeleteCharacterPacket.Read(ref reader).Slot);
    }

    [Fact]
    public void PacketId_Is0x83()
        => Assert.Equal(0x83, DeleteCharacterPacket.PacketId);
}
