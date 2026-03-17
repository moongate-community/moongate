using Moongate.Network.Packets.Incoming.Trading;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class SellListReplyPacketTests
{
    [Test]
    public void TryParse_ShouldReadVendorSerialAndEntries()
    {
        var writer = new SpanWriter(128, true);
        writer.Write((byte)0x9F);
        writer.Write((ushort)0);
        writer.Write(0x00004321u);
        writer.Write((ushort)2);
        writer.Write(0x40000021u);
        writer.Write((short)7);
        writer.Write(0x40000022u);
        writer.Write((short)2);
        writer.WritePacketLength();
        var payload = writer.ToArray();
        writer.Dispose();

        var packet = new SellListReplyPacket();
        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.VendorSerial, Is.EqualTo((Serial)0x00004321u));
                Assert.That(packet.Items.Count, Is.EqualTo(2));
                Assert.That(packet.Items[0].ItemSerial, Is.EqualTo((Serial)0x40000021u));
                Assert.That(packet.Items[0].Amount, Is.EqualTo(7));
                Assert.That(packet.Items[1].ItemSerial, Is.EqualTo((Serial)0x40000022u));
                Assert.That(packet.Items[1].Amount, Is.EqualTo(2));
            }
        );
    }
}
