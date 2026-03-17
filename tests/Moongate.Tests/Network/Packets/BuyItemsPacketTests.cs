using Moongate.Network.Packets.Incoming.Trading;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class BuyItemsPacketTests
{
    [Test]
    public void TryParse_ShouldReadVendorSerialFlagAndEntries()
    {
        var writer = new SpanWriter(128, true);
        writer.Write((byte)0x3B);
        writer.Write((ushort)0);
        writer.Write(0x00001234u);
        writer.Write((byte)0x02);
        writer.Write((byte)0x1A);
        writer.Write(0x40000011u);
        writer.Write((short)3);
        writer.Write((byte)0x1A);
        writer.Write(0x40000012u);
        writer.Write((short)5);
        writer.WritePacketLength();
        var payload = writer.ToArray();
        writer.Dispose();

        var packet = new BuyItemsPacket();
        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.VendorSerial, Is.EqualTo((Serial)0x00001234u));
                Assert.That(packet.Flag, Is.EqualTo((byte)0x02));
                Assert.That(packet.Items.Count, Is.EqualTo(2));
                Assert.That(packet.Items[0].ItemSerial, Is.EqualTo((Serial)0x40000011u));
                Assert.That(packet.Items[0].Amount, Is.EqualTo(3));
                Assert.That(packet.Items[1].ItemSerial, Is.EqualTo((Serial)0x40000012u));
                Assert.That(packet.Items[1].Amount, Is.EqualTo(5));
            }
        );
    }
}
