using System.Text;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public sealed class BulletinBoardSummaryPacketTests
{
    [Test]
    public void Write_ShouldEmitSummaryPayload()
    {
        var packet = new BulletinBoardSummaryPacket
        {
            BoardId = (Serial)0x40000055u,
            MessageId = (Serial)0x40000099u,
            ParentId = (Serial)0x40000011u,
            Poster = "Poster",
            Subject = "Subject",
            PostedAtText = "Day 1 @ 11:28"
        };

        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);
        var bytes = writer.ToArray();
        writer.Dispose();

        Assert.Multiple(
            () =>
            {
                Assert.That(bytes[0], Is.EqualTo(0x71));
                Assert.That(bytes[3], Is.EqualTo(1));
                Assert.That(ToUInt32(bytes, 4), Is.EqualTo(0x40000055u));
                Assert.That(ToUInt32(bytes, 8), Is.EqualTo(0x40000099u));
                Assert.That(ToUInt32(bytes, 12), Is.EqualTo(0x40000011u));
                Assert.That(ReadLengthPrefixedAscii(bytes, 16), Is.EqualTo("Poster"));
            }
        );
    }

    private static string ReadLengthPrefixedAscii(byte[] bytes, int offset)
    {
        var length = bytes[offset];

        return Encoding.ASCII.GetString(bytes, offset + 1, length - 1).TrimEnd('\0');
    }

    private static uint ToUInt32(byte[] bytes, int offset)
        => (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
}
