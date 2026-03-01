using Moongate.Network.Packets.Incoming.Speech;

namespace Moongate.Tests.Network.Packets;

public class OpenChatWindowPacketTests
{
    [Test]
    public void TryParse_ShouldReadFullPayload_ForPacket0xB5()
    {
        var packet = new OpenChatWindowPacket();
        var data = new byte[64];
        data[0] = 0xB5;
        data[1] = 0x45;
        data[2] = 0x4E;
        data[3] = 0x55;

        var parsed = packet.TryParse(data);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.Payload.Length, Is.EqualTo(63));
                Assert.That(packet.Payload.Span[0], Is.EqualTo(0x45));
                Assert.That(packet.Payload.Span[1], Is.EqualTo(0x4E));
                Assert.That(packet.Payload.Span[2], Is.EqualTo(0x55));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenLengthIsNot64()
    {
        var packet = new OpenChatWindowPacket();
        var data = new byte[63];
        data[0] = 0xB5;

        var parsed = packet.TryParse(data);

        Assert.That(parsed, Is.False);
    }
}
