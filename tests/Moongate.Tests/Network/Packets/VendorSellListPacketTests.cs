using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Trading;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class VendorSellListPacketTests
{
    [Test]
    public void Write_ShouldSerializeClassicVendorSellListPacket()
    {
        var packet = new VendorSellListPacket
        {
            VendorSerial = (Serial)0x00001234u,
            Entries =
            {
                new()
                {
                    ItemSerial = (Serial)0x40000051u,
                    ItemId = 0x0EED,
                    Hue = 0x0444,
                    Amount = 50,
                    Price = 1,
                    Name = "gold"
                }
            }
        };

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0x9E));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo((ushort)data.Length));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(3, 4)), Is.EqualTo(0x00001234u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(7, 2)), Is.EqualTo((ushort)1));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(9, 4)), Is.EqualTo(0x40000051u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(13, 2)), Is.EqualTo((ushort)0x0EED));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(19, 2)), Is.EqualTo((ushort)1));
            }
        );
    }

    private static byte[] Write(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
