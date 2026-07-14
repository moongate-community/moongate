using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network;

public class OutgoingPacketsTests
{
    [Fact]
    public void LoginDeniedPacket_Write_ProducesWireBytes()
    {
        var packet = new LoginDeniedPacket(LoginDeniedReasonType.IncorrectCredentials);

        var writer = new SpanWriter(stackalloc byte[2]);
        packet.Write(ref writer);

        Assert.Equal(new byte[] { 0x82, 0x00 }, writer.Span.ToArray());
    }

    [Fact]
    public void MovementAckPacket_Write_ProducesWireBytes()
    {
        var packet = new MovementAckPacket(0x0A, 1);

        var writer = new SpanWriter(stackalloc byte[3]);
        packet.Write(ref writer);

        Assert.Equal(new byte[] { 0x22, 0x0A, 0x01 }, writer.Span.ToArray());
    }

    [Fact]
    public void SupportFeaturesPacket_Write_ProducesModernFlagsBigEndian()
    {
        var packet = new SupportFeaturesPacket(FeatureFlagType.Modern);

        var writer = new SpanWriter(stackalloc byte[5]);
        packet.Write(ref writer);

        // 0xB9 then the 4-byte modern feature set (0x00FF92F8) big-endian.
        Assert.Equal(new byte[] { 0xB9, 0x00, 0xFF, 0x92, 0xF8 }, writer.Span.ToArray());
    }
}
