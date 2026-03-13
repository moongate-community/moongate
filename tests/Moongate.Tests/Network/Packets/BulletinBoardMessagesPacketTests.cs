using System.Text;
using Moongate.Network.Packets.Data.BulletinBoard;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Spans;

namespace Moongate.Tests.Network.Packets;

public sealed class BulletinBoardMessagesPacketTests
{
    private delegate void PacketPayloadWriter(ref SpanWriter writer);

    [Test]
    public void TryParse_WithRequestMessage_ShouldReadBoardAndMessageIds()
    {
        var raw = BuildPacket(
            BulletinBoardSubcommand.RequestMessage,
            (ref SpanWriter writer) =>
            {
                writer.Write(0x40000010u);
                writer.Write(0x40000020u);
            }
        );

        var packet = new BulletinBoardMessagesPacket();

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.TryParse(raw), Is.True);
                Assert.That(packet.Subcommand, Is.EqualTo(BulletinBoardSubcommand.RequestMessage));
                Assert.That(packet.BoardId, Is.EqualTo(0x40000010u));
                Assert.That(packet.MessageId, Is.EqualTo(0x40000020u));
            }
        );
    }

    [Test]
    public void TryParse_WithRequestMessageSummary_ShouldReadBoardAndMessageIds()
    {
        var raw = BuildPacket(
            BulletinBoardSubcommand.RequestMessageSummary,
            (ref SpanWriter writer) =>
            {
                writer.Write(0x40000011u);
                writer.Write(0x40000021u);
            }
        );

        var packet = new BulletinBoardMessagesPacket();

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.TryParse(raw), Is.True);
                Assert.That(packet.Subcommand, Is.EqualTo(BulletinBoardSubcommand.RequestMessageSummary));
                Assert.That(packet.BoardId, Is.EqualTo(0x40000011u));
                Assert.That(packet.MessageId, Is.EqualTo(0x40000021u));
            }
        );
    }

    [Test]
    public void TryParse_WithPostMessage_ShouldReadParentSubjectAndBody()
    {
        var raw = BuildPacket(
            BulletinBoardSubcommand.PostMessage,
            (ref SpanWriter writer) =>
            {
                writer.Write(0x40000012u);
                writer.Write(0x40000022u);
                WriteAsciiNull(ref writer, "Subject");
                writer.Write((byte)2);
                WriteAsciiNull(ref writer, "line one");
                WriteAsciiNull(ref writer, "line two");
            }
        );

        var packet = new BulletinBoardMessagesPacket();

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.TryParse(raw), Is.True);
                Assert.That(packet.Subcommand, Is.EqualTo(BulletinBoardSubcommand.PostMessage));
                Assert.That(packet.BoardId, Is.EqualTo(0x40000012u));
                Assert.That(packet.ParentId, Is.EqualTo(0x40000022u));
                Assert.That(packet.Subject, Is.EqualTo("Subject"));
                Assert.That(packet.BodyLines, Is.EqualTo(new[] { "line one", "line two" }));
            }
        );
    }

    [Test]
    public void TryParse_WithRemoveMessage_ShouldReadBoardAndMessageIds()
    {
        var raw = BuildPacket(
            BulletinBoardSubcommand.RemovePostedMessage,
            (ref SpanWriter writer) =>
            {
                writer.Write(0x40000013u);
                writer.Write(0x40000023u);
            }
        );

        var packet = new BulletinBoardMessagesPacket();

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.TryParse(raw), Is.True);
                Assert.That(packet.Subcommand, Is.EqualTo(BulletinBoardSubcommand.RemovePostedMessage));
                Assert.That(packet.BoardId, Is.EqualTo(0x40000013u));
                Assert.That(packet.MessageId, Is.EqualTo(0x40000023u));
            }
        );
    }

    [Test]
    public void TryParse_WithServerOnlySubcommand_ShouldFail()
    {
        var raw = BuildPacket(BulletinBoardSubcommand.DisplayBulletinBoard, (ref SpanWriter _) => { });
        var packet = new BulletinBoardMessagesPacket();

        Assert.That(packet.TryParse(raw), Is.False);
    }

    private static byte[] BuildPacket(BulletinBoardSubcommand subcommand, PacketPayloadWriter payloadWriter)
    {
        var writer = new SpanWriter(256, true);
        writer.Write((byte)0x71);
        writer.Write((ushort)0);
        writer.Write((byte)subcommand);
        payloadWriter(ref writer);
        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        return bytes;
    }

    private static void WriteAsciiNull(ref SpanWriter writer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        writer.Write((byte)(bytes.Length + 1));
        writer.Write(bytes);
        writer.Write((byte)0x00);
    }
}
