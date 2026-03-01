using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class ChatCommandPacketTests
{
    [Test]
    public void Write_ShouldSerializeOpenChatWindowCommand()
    {
        var packet = new ChatCommandPacket(ChatCommandType.OpenChatWindow, "PlayerOne");
        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0xB2));
                Assert.That((ushort)((data[1] << 8) | data[2]), Is.EqualTo(data.Length));
                Assert.That((ushort)((data[3] << 8) | data[4]), Is.EqualTo((ushort)ChatCommandType.OpenChatWindow));
                Assert.That(data[5], Is.EqualTo((byte)'E'));
                Assert.That(data[6], Is.EqualTo((byte)'N'));
                Assert.That(data[7], Is.EqualTo((byte)'U'));
            }
        );
    }
}
