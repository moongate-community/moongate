using System.Buffers.Binary;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Network.Packets;

public sealed class DisplaySpellbookAndContentsPacketTests
{
    [Test]
    public void Write_ShouldEmitSpellbookDisplayAndContentPayload()
    {
        var spellbook = new UOItemEntity
        {
            Id = (Serial)0x40000021u,
            ItemId = 0x0EFA
        };
        var packet = new DisplaySpellbookAndContentsPacket(spellbook, 0x0000000000000003UL);
        var writer = new SpanWriter(64, true);

        packet.Write(ref writer);

        var data = writer.ToArray();
        writer.Dispose();

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0x24));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(1, 4)), Is.EqualTo(0x40000021u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(5, 2)), Is.EqualTo(0xFFFF));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(7, 2)), Is.EqualTo(0x007D));
                Assert.That(data[9], Is.EqualTo(0xBF));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10, 2)), Is.EqualTo((ushort)23));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(12, 2)), Is.EqualTo((ushort)0x001B));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(14, 2)), Is.EqualTo((ushort)0x0001));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4)), Is.EqualTo(0x40000021u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(20, 2)), Is.EqualTo((ushort)0x0EFA));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(22, 2)), Is.EqualTo((ushort)1));
                Assert.That(data[24], Is.EqualTo(0x03));
                Assert.That(data[25], Is.EqualTo(0x00));
            }
        );
    }
}
