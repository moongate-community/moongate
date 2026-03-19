using System.Buffers.Binary;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public sealed class CorpseClothingPacketTests
{
    [Test]
    public void Write_WhenCorpseContainsEquippedItems_ShouldSerializeLayerEntriesAndTerminator()
    {
        var corpse = new UOItemEntity
        {
            Id = (Serial)0x40000010u,
            ItemId = 0x2006
        };
        var chest = new UOItemEntity
        {
            Id = (Serial)0x40000011u,
            ItemId = 0x1415,
            ParentContainerId = corpse.Id
        };
        chest.SetCustomInteger("corpse_equipped_layer", (byte)ItemLayerType.InnerTorso);
        corpse.AddItem(chest, Point2D.Zero);

        var packet = new CorpseClothingPacket(corpse);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0x89));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo(13));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(3, 4)), Is.EqualTo(0x40000010u));
                Assert.That(data[7], Is.EqualTo((byte)ItemLayerType.InnerTorso + 1));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8, 4)), Is.EqualTo(0x40000011u));
                Assert.That(data[12], Is.EqualTo((byte)ItemLayerType.Invalid));
            }
        );
    }

    [Test]
    public void Write_WhenCorpseContainsNonEquippedItems_ShouldSkipThem()
    {
        var corpse = new UOItemEntity
        {
            Id = (Serial)0x40000020u,
            ItemId = 0x2006
        };
        corpse.AddItem(
            new UOItemEntity
            {
                Id = (Serial)0x40000021u,
                ItemId = 0x0EED,
                ParentContainerId = corpse.Id
            },
            Point2D.Zero
        );

        var packet = new CorpseClothingPacket(corpse);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0x89));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo(8));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(3, 4)), Is.EqualTo(0x40000020u));
                Assert.That(data[7], Is.EqualTo((byte)ItemLayerType.Invalid));
            }
        );
    }

    private static byte[] Write(CorpseClothingPacket packet)
    {
        var writer = new SpanWriter(64, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
