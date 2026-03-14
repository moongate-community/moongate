using System.Text;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public sealed class BulletinBoardDisplayPacketTests
{
    [Test]
    public void Write_ShouldEmitDisplayBoardPayload()
    {
        var packet = new BulletinBoardDisplayPacket((Serial)0x40000055u, "Town Board");
        var writer = new SpanWriter(128, true);
        packet.Write(ref writer);
        var bytes = writer.ToArray();
        writer.Dispose();

        Assert.Multiple(
            () =>
            {
                Assert.That(bytes[0], Is.EqualTo(0x71));
                Assert.That((bytes[1] << 8) | bytes[2], Is.EqualTo(bytes.Length));
                Assert.That(bytes[3], Is.EqualTo(0));
                Assert.That(ToUInt32(bytes, 4), Is.EqualTo(0x40000055u));
                Assert.That(Encoding.ASCII.GetString(bytes, 8, 22).TrimEnd('\0'), Is.EqualTo("Town Board"));
                Assert.That(ToUInt32(bytes, 30), Is.EqualTo(BulletinBoardDisplayPacket.DefaultListSerial));
                Assert.That(ToUInt32(bytes, 34), Is.EqualTo(0u));
            }
        );
    }

    private static uint ToUInt32(byte[] bytes, int offset)
        => (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
}
