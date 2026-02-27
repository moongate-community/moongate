using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class TargetCursorCommandsPacketTests
{
    [Test]
    public void CreateCancelCurrentTarget_ShouldBuildExpectedPacket()
    {
        var packet = TargetCursorCommandsPacket.CreateCancelCurrentTarget();
        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.CursorTarget, Is.EqualTo(TargetCursorSelectionType.SelectObject));
                Assert.That(packet.CursorId, Is.EqualTo((Serial)0u));
                Assert.That(packet.CursorType, Is.EqualTo(TargetCursorType.CancelCurrentTargeting));
                Assert.That(
                    data,
                    Is.EqualTo(
                        new byte[19]
                        {
                            0x6C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00
                        }
                    )
                );
            }
        );
    }

    [Test]
    public void TryParse_ShouldReadAllFields()
    {
        var packet = new TargetCursorCommandsPacket();
        var payload = new byte[]
        {
            0x6C,
            0x00,
            0xAA, 0xBB, 0xCC, 0xDD,
            0x02,
            0x40, 0x00, 0x00, 0x42,
            0x01, 0x02,
            0x03, 0x04,
            0x00,
            0xFE,
            0x0E, 0xED
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.CursorTarget, Is.EqualTo(TargetCursorSelectionType.SelectObject));
                Assert.That(packet.CursorId, Is.EqualTo((Serial)0xAABBCCDDu));
                Assert.That(packet.CursorType, Is.EqualTo(TargetCursorType.Helpful));
                Assert.That(packet.ClickedOnId, Is.EqualTo((Serial)0x40000042u));
                Assert.That(packet.Location, Is.EqualTo(new Point3D(0x0102, 0x0304, -2)));
                Assert.That(packet.Unknown, Is.EqualTo(0x00));
                Assert.That(packet.Graphic, Is.EqualTo(0x0EED));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new TargetCursorCommandsPacket(
            TargetCursorSelectionType.SelectLocation,
            (Serial)0x01020304u,
            TargetCursorType.Harmful
        )
        {
            ClickedOnId = (Serial)0x40000010u,
            Location = new(0x1122, 0x3344, -2),
            Unknown = 0x00,
            Graphic = 0x5566
        };

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(19));
                Assert.That(data[0], Is.EqualTo(0x6C));
                Assert.That(data[1], Is.EqualTo((byte)TargetCursorSelectionType.SelectLocation));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(2, 4)), Is.EqualTo(0x01020304u));
                Assert.That(data[6], Is.EqualTo((byte)TargetCursorType.Harmful));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(7, 4)), Is.EqualTo(0x40000010u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(11, 2)), Is.EqualTo(0x1122));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(13, 2)), Is.EqualTo(0x3344));
                Assert.That(data[15], Is.EqualTo(0x00));
                Assert.That(unchecked((sbyte)data[16]), Is.EqualTo(-2));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(17, 2)), Is.EqualTo(0x5566));
            }
        );
    }

    private static byte[] Write(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(32, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
