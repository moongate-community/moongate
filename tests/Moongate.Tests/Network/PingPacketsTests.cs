using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network;

public class PingPacketsTests
{
    [Fact]
    public void PingAckPacket_Write_EchoesSequence()
    {
        var writer = new SpanWriter(4);
        new PingAckPacket(0x2A).Write(ref writer);

        Assert.Equal(new byte[] { 0x73, 0x2A }, writer.Span.ToArray());
    }

    [Fact]
    public void PingPacket_Read_ParsesSequenceByte()
    {
        var reader = new SpanReader(new byte[] { 0x73, 0x2A });

        var packet = PingPacket.Read(ref reader);

        Assert.Equal(0x2A, packet.Sequence);
    }
}
