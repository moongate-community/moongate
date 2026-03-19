using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Spans;
using Moongate.UO.Data.Types.Speech;

namespace Moongate.Tests.Network.Packets;

public class ChatTextPacketTests
{
    [Test]
    public void TryParse_ShouldPopulateFields_ForChatMessageAction()
    {
        var packet = new ChatTextPacket();
        var data = BuildPayload("ENU", ChatActionType.Message, "hello conference");

        var parsed = packet.TryParse(data);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.Language, Is.EqualTo("ENU"));
                Assert.That(packet.ActionId, Is.EqualTo(ChatActionType.Message));
                Assert.That(packet.Payload, Is.EqualTo("hello conference"));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenDeclaredLengthDoesNotMatchBuffer()
    {
        var packet = new ChatTextPacket();
        var data = BuildPayload("ENU", ChatActionType.Message, "hello conference");
        data[1] = 0x00;
        data[2] = 0x03;

        Assert.That(packet.TryParse(data), Is.False);
    }

    [Test]
    public void TryParse_ShouldSupportEmptyUnicodePayload_ForCloseAction()
    {
        var packet = new ChatTextPacket();
        var data = BuildPayload("ENU", ChatActionType.Close, string.Empty);

        var parsed = packet.TryParse(data);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.Language, Is.EqualTo("ENU"));
                Assert.That(packet.ActionId, Is.EqualTo(ChatActionType.Close));
                Assert.That(packet.Payload, Is.EqualTo(string.Empty));
            }
        );
    }

    private static byte[] BuildPayload(string language, ChatActionType actionId, string payload)
    {
        var writer = new SpanWriter(128, true);

        writer.Write((byte)0xB3);
        writer.Write((ushort)0);
        writer.WriteAscii(language, 4);
        writer.Write((short)actionId);
        writer.WriteBigUniNull(payload);
        writer.WritePacketLength();

        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
