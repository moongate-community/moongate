using System.Text;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public sealed class BulletinBoardMessagePacketTests
{
    [Test]
    public void Write_ShouldEmitFullMessagePayload()
    {
        var packet = new BulletinBoardMessagePacket
        {
            BoardId = (Serial)0x40000055u,
            MessageId = (Serial)0x40000099u,
            Poster = "Poster",
            Subject = "Subject",
            PostedAtText = "Day 1 @ 11:28"
        };
        packet.BodyLines.AddRange(["line one", "line two"]);

        var writer = new SpanWriter(512, true);
        packet.Write(ref writer);
        var bytes = writer.ToArray();
        writer.Dispose();

        Assert.Multiple(
            () =>
            {
                Assert.That(bytes[0], Is.EqualTo(0x71));
                Assert.That(bytes[3], Is.EqualTo(2));
                Assert.That(ToUInt32(bytes, 4), Is.EqualTo(0x40000055u));
                Assert.That(ToUInt32(bytes, 8), Is.EqualTo(0x40000099u));
                Assert.That(Encoding.ASCII.GetString(bytes).Contains("line one\0"), Is.True);
                Assert.That(Encoding.ASCII.GetString(bytes).Contains("line two\0"), Is.True);
            }
        );
    }

    private static uint ToUInt32(byte[] bytes, int offset)
        => (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
}
