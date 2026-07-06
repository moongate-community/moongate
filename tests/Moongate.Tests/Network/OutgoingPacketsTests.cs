using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network;

public class OutgoingPacketsTests
{
    [Fact]
    public void MovementAckPacket_Write_ProducesWireBytes()
    {
        var packet = new MovementAckPacket(Sequence: 0x0A, Notoriety: 1);

        var writer = new SpanWriter(stackalloc byte[3]);
        packet.Write(ref writer);

        Assert.Equal(new byte[] { 0x22, 0x0A, 0x01 }, writer.Span.ToArray());
    }

    [Fact]
    public void LoginDeniedPacket_Write_ProducesWireBytes()
    {
        var packet = new LoginDeniedPacket(LoginDeniedReasonType.IncorrectCredentials);

        var writer = new SpanWriter(stackalloc byte[2]);
        packet.Write(ref writer);

        Assert.Equal(new byte[] { 0x82, 0x00 }, writer.Span.ToArray());
    }
}
