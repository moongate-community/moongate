using System.Buffers.Binary;
using Moongate.Core.Types;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Outgoing;

public class MoveRejectPacketTests
{
    [Fact]
    public void Write_EncodesSequenceXYDirectionZInOrder()
    {
        var bytes = Serialize(new MoveRejectPacket(7, 100, 200, DirectionType.South, 5));

        Assert.Equal(7, bytes[1]);
        Assert.Equal((ushort)100, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(2)));
        Assert.Equal((ushort)200, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4)));
        Assert.Equal((byte)DirectionType.South, bytes[6]);
        Assert.Equal(5, (sbyte)bytes[7]);
    }

    [Fact]
    public void Write_ProducesEightBytesWithTheOpcodeFirst()
    {
        var bytes = Serialize(new MoveRejectPacket(7, 100, 200, DirectionType.South, 5));

        Assert.Equal(8, bytes.Length);
        Assert.Equal(0x21, bytes[0]);
    }

    private static byte[] Serialize(IOutgoingPacket packet)
    {
        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);

        return writer.Span.ToArray();
    }
}
