using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class SingleClickPacketTests
{
    private static SingleClickPacket Read(uint serial)
    {
        var buffer = new byte[5];
        buffer[0] = 0x09;
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(1), serial);

        var reader = new SpanReader(buffer);

        return SingleClickPacket.Read(ref reader);
    }

    [Fact]
    public void Read_ParsesTheTargetSerial()
        => Assert.Equal(0x1234u, Read(0x1234).Target.Value);

    [Fact]
    public void PacketId_Is0x09()
        => Assert.Equal(0x09, SingleClickPacket.PacketId);
}
