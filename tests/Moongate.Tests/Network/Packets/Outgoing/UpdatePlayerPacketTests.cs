using System.Buffers.Binary;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Outgoing;

public class UpdatePlayerPacketTests
{
    private static byte[] Serialize(IOutgoingPacket packet)
    {
        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);

        return writer.Span.ToArray();
    }

    [Fact]
    public void Write_ProducesSeventeenBytesWithTheOpcodeFirst()
    {
        var bytes = Serialize(
            new UpdatePlayerPacket(new Serial(0x1000), 400, 100, 200, 5, DirectionType.East, new(0x10), 0, NotorietyType.Innocent)
        );

        Assert.Equal(17, bytes.Length);
        Assert.Equal(0x77, bytes[0]);
    }

    [Fact]
    public void Write_EncodesFieldsInOrder()
    {
        var bytes = Serialize(
            new UpdatePlayerPacket(new Serial(0x1000), 400, 100, 200, 5, DirectionType.East, new(0x10), 3, NotorietyType.Criminal)
        );

        Assert.Equal(0x1000u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)));
        Assert.Equal((ushort)400, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(5)));
        Assert.Equal((ushort)100, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(7)));
        Assert.Equal((ushort)200, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(9)));
        Assert.Equal(5, (sbyte)bytes[11]);
        Assert.Equal((byte)DirectionType.East, bytes[12]);
        Assert.Equal((ushort)0x10, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(13)));
        Assert.Equal(3, bytes[15]);
        Assert.Equal((byte)NotorietyType.Criminal, bytes[16]);
    }
}
