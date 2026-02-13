using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

public class LoginDeniedPacketTests
{
    [Test]
    public void LoginDeniedPacket_ShouldHaveOpCode0x82()
    {
        var packet = new LoginDeniedPacket();
        Assert.That(packet.OpCode, Is.EqualTo(0x82));
    }

    [Test]
    public void LoginDeniedPacket_ShouldSerializeReason()
    {
        var packet = new LoginDeniedPacket { Reason = 1 };
        using var writer = new SpanWriter(10);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0x82));
    }

    [Test]
    public void LoginDeniedPacket_ShouldHandleValidReasons()
    {
        for (byte i = 0; i <= 5; i++)
        {
            var packet = new LoginDeniedPacket { Reason = i };
            using var writer = new SpanWriter(10);
            var result = packet.Write(writer);
            Assert.That(result.Length, Is.EqualTo(2), $"Reason {i} should serialize to 2 bytes");
        }
    }

    [Test]
    public void LoginDeniedPacket_ShouldHaveReasonProperty()
    {
        var packet = new LoginDeniedPacket();
        packet.Reason = 3;
        Assert.That(packet.Reason, Is.EqualTo(3));
    }

    [Test]
    public void LoginDeniedPacket_ShouldSerializeWithDefaultReason()
    {
        var packet = new LoginDeniedPacket();
        using var writer = new SpanWriter(10);
        var result = packet.Write(writer);
        Assert.That(result.Length, Is.EqualTo(2));
    }

    [Test]
    public void LoginDeniedPacket_ShouldSerializeWithZeroReason()
    {
        var packet = new LoginDeniedPacket { Reason = 0 };
        using var writer = new SpanWriter(10);
        var result = packet.Write(writer);
        Assert.That(result.Span[1], Is.EqualTo(0));
    }
}
