using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class DoubleClickPacketTests
{
    private static DoubleClickPacket Read(uint serial)
    {
        var buffer = new byte[5];
        buffer[0] = 0x06;
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(1), serial);

        var reader = new SpanReader(buffer);

        return DoubleClickPacket.Read(ref reader);
    }

    [Fact]
    public void Read_ParsesTheTargetSerial()
        => Assert.Equal(0x40000009u, Read(0x40000009).Target.Value);

    [Fact]
    public void PacketId_Is0x06()
        => Assert.Equal(0x06, DoubleClickPacket.PacketId);
}
