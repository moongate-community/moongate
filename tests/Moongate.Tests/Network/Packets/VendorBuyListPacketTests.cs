using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Trading;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class VendorBuyListPacketTests
{
    [Test]
    public void Write_ShouldSerializeClassicVendorBuyListPacket()
    {
        var packet = new VendorBuyListPacket
        {
            ShopContainerSerial = (Serial)0x40000021u,
            Entries =
            {
                new() { Price = 25, Description = "dagger" },
                new() { Price = 45, Description = "longsword" }
            }
        };

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0x74));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo((ushort)data.Length));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(3, 4)), Is.EqualTo(0x40000021u));
                Assert.That(data[7], Is.EqualTo(2));
                Assert.That(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(8, 4)), Is.EqualTo(25));
                Assert.That(data[12], Is.EqualTo((byte)"dagger".Length + 1));
                Assert.That(data.AsSpan(13, 6).ToArray(), Is.EqualTo("dagger"u8.ToArray()));
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
