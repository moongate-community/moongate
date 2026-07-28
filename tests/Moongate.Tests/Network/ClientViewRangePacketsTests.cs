using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network;

public class ClientViewRangePacketsTests
{
    [Fact]
    public void ClientViewRangeAckPacket_Write_EmitsOpcodeAndRange()
    {
        var writer = new SpanWriter(4);
        new ClientViewRangeAckPacket(18).Write(ref writer);

        Assert.Equal(new byte[] { 0xC8, 0x12 }, writer.Span.ToArray());
    }

    [Fact]
    public void ClientViewRangePacket_Read_ParsesRangeByte()
    {
        var reader = new SpanReader(new byte[] { 0xC8, 0x0A });

        var packet = ClientViewRangePacket.Read(ref reader);

        Assert.Equal(10, packet.Range);
    }
}
