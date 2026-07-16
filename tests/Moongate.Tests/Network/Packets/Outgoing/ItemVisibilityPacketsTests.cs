using System.Buffers.Binary;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.UO.Data.Hues;
using Moongate.Ultima.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Outgoing;

/// <summary>
/// Byte-layout tests for the packets that make items visible. The expected offsets and lengths come
/// from ModernUO's outgoing item, container and entity packets, in their modern-client form.
/// </summary>
public class ItemVisibilityPacketsTests
{
    private static byte[] Serialize(IOutgoingPacket packet)
    {
        var writer = new SpanWriter(512, true);
        packet.Write(ref writer);

        return writer.Span.ToArray();
    }

    [Fact]
    public void DeleteObject_IsFiveBytesCarryingTheSerial()
    {
        var bytes = Serialize(new DeleteObjectPacket(new Serial(0x40000005)));

        Assert.Equal(5, bytes.Length);
        Assert.Equal(0x1D, bytes[0]);
        Assert.Equal(0x40000005u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)));
    }

    [Fact]
    public void LiftReject_IsTwoBytesCarryingTheReason()
    {
        var bytes = Serialize(new LiftRejectPacket(LiftRejectReasonType.OutOfRange));

        Assert.Equal(2, bytes.Length);
        Assert.Equal(0x27, bytes[0]);
        Assert.Equal((byte)LiftRejectReasonType.OutOfRange, bytes[1]);
    }

    [Fact]
    public void DrawContainer_IsNineBytesWithTheModernTrailer()
    {
        var bytes = Serialize(new DrawContainerPacket(new Serial(0x40000009), 0x003C));

        Assert.Equal(9, bytes.Length);
        Assert.Equal(0x24, bytes[0]);
        Assert.Equal(0x40000009u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)));
        Assert.Equal(0x003C, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(5)));
        Assert.Equal(0x7D, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(7))); // modern clients only
    }

    [Fact]
    public void WornItem_IsFifteenBytesCarryingLayerAndOwner()
    {
        var bytes = Serialize(
            new WornItemPacket(new Serial(0x40000005), 0x1517, LayerType.Shirt, new Serial(0x64), new Hue(0x10))
        );

        Assert.Equal(15, bytes.Length);
        Assert.Equal(0x2E, bytes[0]);
        Assert.Equal(0x40000005u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)));
        Assert.Equal(0x1517, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(5)));
        Assert.Equal(0x00, bytes[7]);                       // itemId offset
        Assert.Equal((byte)LayerType.Shirt, bytes[8]);
        Assert.Equal(0x64u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(9)));
        Assert.Equal(0x10, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(13)));
    }

    [Fact]
    public void AddItemToContainer_IsTwentyOneBytesWithTheGridLocationByte()
    {
        var bytes = Serialize(
            new AddItemToContainerPacket(
                new Serial(0x40000005),
                0x0F51,
                3,
                new Point2D(44, 65),
                new Serial(0x40000001),
                new Hue(0x21)
            )
        );

        Assert.Equal(21, bytes.Length);
        Assert.Equal(0x25, bytes[0]);
        Assert.Equal(0x40000005u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)));
        Assert.Equal(0x0F51, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(5)));
        Assert.Equal(0x00, bytes[7]);                                             // itemId offset
        Assert.Equal(3, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8)));   // amount
        Assert.Equal(44, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(10)));  // x in the gump
        Assert.Equal(65, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(12)));  // y in the gump
        Assert.Equal(0x00, bytes[14]);                                            // grid location
        Assert.Equal(0x40000001u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(15)));
        Assert.Equal(0x21, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(19)));
    }

    [Fact]
    public void ContainerContent_HasFiveByteHeaderAndTwentyBytesPerItem()
    {
        var container = new Serial(0x40000001);
        var bytes = Serialize(
            new ContainerContentPacket(
                container,
                [
                    new(new Serial(0x40000005), 0x0F51, 1, new Point2D(44, 65), new Hue(0)),
                    new(new Serial(0x40000006), 0x1517, 7, new Point2D(90, 30), new Hue(0x21))
                ]
            )
        );

        Assert.Equal(5 + 20 * 2, bytes.Length);
        Assert.Equal(0x3C, bytes[0]);
        Assert.Equal(5 + 20 * 2, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1))); // length
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(3)));          // count

        // Second entry, to prove the stride: it starts at 5 + 20.
        var second = bytes.AsSpan(25);
        Assert.Equal(0x40000006u, BinaryPrimitives.ReadUInt32BigEndian(second));
        Assert.Equal(0x1517, BinaryPrimitives.ReadUInt16BigEndian(second[4..]));
        Assert.Equal(7, BinaryPrimitives.ReadUInt16BigEndian(second[7..]));
        Assert.Equal(90, BinaryPrimitives.ReadInt16BigEndian(second[9..]));
        Assert.Equal(30, BinaryPrimitives.ReadInt16BigEndian(second[11..]));
        // Every entry repeats the container serial — that is how the client places it.
        Assert.Equal(0x40000001u, BinaryPrimitives.ReadUInt32BigEndian(second[14..]));
        Assert.Equal(0x21, BinaryPrimitives.ReadUInt16BigEndian(second[18..]));
    }

    [Fact]
    public void ContainerContent_WithNoItems_IsJustTheHeader()
    {
        var bytes = Serialize(new ContainerContentPacket(new Serial(0x40000001), []));

        Assert.Equal(5, bytes.Length);
        Assert.Equal(5, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(3)));
    }

    [Fact]
    public void WorldItem_IsTwentySixBytesInTheModernForm()
    {
        var bytes = Serialize(
            new WorldItemPacket(new Serial(0x40000005), 0x0F51, 5, new Point3D(1000, 2000, -20), new Hue(0x21))
        );

        Assert.Equal(26, bytes.Length);
        Assert.Equal(0xF3, bytes[0]);
        Assert.Equal(0x01, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(1)));  // command
        Assert.Equal(0x00, bytes[3]);                                              // entity type: item
        Assert.Equal(0x40000005u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4)));
        Assert.Equal(0x0F51, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8)));
        Assert.Equal(0x00, bytes[10]);                                             // facing
        Assert.Equal(5, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(11)));    // amount min
        Assert.Equal(5, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(13)));    // amount max
        Assert.Equal(1000, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(15)));
        Assert.Equal(2000, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(17)));
        Assert.Equal(-20, (sbyte)bytes[19]);                                       // z is signed
        Assert.Equal(0x00, bytes[20]);                                             // light
        Assert.Equal(0x21, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(21)));
        Assert.Equal(0x00, bytes[23]);                                             // flags
        Assert.Equal(0x00, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(24))); // modern trailer
    }

    [Fact]
    public void WorldItem_MasksTheCoordinatesTheWayTheClientPacksThem()
    {
        // The top bit of x and the top two of y are not part of the coordinate.
        var bytes = Serialize(
            new WorldItemPacket(new Serial(1), 1, 1, new Point3D(0xFFFF, 0xFFFF, 0), new Hue(0))
        );

        Assert.Equal(0x7FFF, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(15)));
        Assert.Equal(0x3FFF, BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(17)));
    }
}
