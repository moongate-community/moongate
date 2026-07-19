using System.Buffers.Binary;
using System.Text;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Outgoing;

public class PaperdollPacketTests
{
    [Theory, InlineData(false, false, 0x00), InlineData(true, false, 0x01), InlineData(false, true, 0x02),
     InlineData(true, true, 0x03)]

    // warmode
    // canLift
    public void Write_FlagsCombineWarmodeAndCanLift(bool warmode, bool canLift, byte expected)
        => Assert.Equal(expected, Serialize(new PaperdollPacket(new(1), "X", warmode, canLift))[65]);

    [Fact]
    public void Write_ProducesTheFixed66ByteLayout()
    {
        var bytes = Serialize(new PaperdollPacket(new(0x64), "Hero", false, false));

        Assert.Equal(66, bytes.Length);
        Assert.Equal(0x88, bytes[0]);
        Assert.Equal(0x64u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)));
        Assert.Equal("Hero", Encoding.ASCII.GetString(bytes, 5, 60).TrimEnd('\0'));
        Assert.Equal(0x00, bytes[65]);
    }

    private static byte[] Serialize(IOutgoingPacket packet)
    {
        var writer = new SpanWriter(128, true);
        packet.Write(ref writer);

        return writer.Span.ToArray();
    }
}
