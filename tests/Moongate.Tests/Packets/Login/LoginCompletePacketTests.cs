using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

public class LoginCompletePacketTests
{
    [Test]
    public void LoginCompletePacket_ShouldHaveOpCode0x55()
    {
        var packet = new LoginCompletePacket();
        Assert.That(packet.OpCode, Is.EqualTo(0x55));
    }

    [Test]
    public void LoginCompletePacket_ShouldSerializeSuccessfully()
    {
        var packet = new LoginCompletePacket();
        using var writer = new SpanWriter(10);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0x55));
    }

    [Test]
    public void LoginCompletePacket_ShouldHaveMinimalLength()
    {
        var packet = new LoginCompletePacket();
        using var writer = new SpanWriter(10);
        var result = packet.Write(writer);

        // Should be just OpCode (1-2 bytes)
        Assert.That(result.Length, Is.LessThanOrEqualTo(2));
    }

    [Test]
    public void LoginCompletePacket_ShouldSerializeConsistently()
    {
        var packet = new LoginCompletePacket();

        using var writer1 = new SpanWriter(10);
        var result1 = packet.Write(writer1);

        using var writer2 = new SpanWriter(10);
        var result2 = packet.Write(writer2);

        Assert.That(result1.Length, Is.EqualTo(result2.Length));
    }
}
