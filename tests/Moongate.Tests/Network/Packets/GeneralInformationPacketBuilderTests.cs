using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class GeneralInformationPacketBuilderTests
{
    [Test]
    public void Create_ShouldThrow_WhenPayloadInvalid()
        => Assert.Throws<ArgumentException>(
            () => GeneralInformationPacketBuilder.Create(GeneralInformationSubcommandType.SetCursorHueSetMap, [])
        );

    [Test]
    public void CreateMountSpeed_ShouldBuildExpectedPacket()
    {
        var packet = GeneralInformationPacketBuilder.CreateMountSpeed(1);
        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0xBF));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo((ushort)6));
                Assert.That(
                    BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(3, 2)),
                    Is.EqualTo((ushort)GeneralInformationSubcommandType.MountSpeed)
                );
                Assert.That(data[5], Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void CreateSetCursorHueSetMap_ShouldBuildExpectedPacket()
    {
        var packet = GeneralInformationPacketBuilder.CreateSetCursorHueSetMap(2);
        var data = Write(packet);

        Assert.That(data, Is.EqualTo(new byte[] { 0xBF, 0x00, 0x06, 0x00, 0x08, 0x02 }));
    }

    [Test]
    public void CreateWrestlingStun_ShouldBuildEmptyPayloadPacket()
    {
        var packet = GeneralInformationPacketBuilder.CreateWrestlingStun();
        var data = Write(packet);

        Assert.That(data, Is.EqualTo(new byte[] { 0xBF, 0x00, 0x05, 0x00, 0x0A }));
    }

    [Test]
    public void CreateDisplayPopupContextMenu2D_ShouldBuildExpectedPacket()
    {
        var packet = GeneralInformationPacketBuilder.CreateDisplayPopupContextMenu2D(
            0x00000009,
            [
                new(1, 3006123),
                new(2, 3006103),
                new(3, 3006104, Flags: 0x01)
            ]
        );
        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0xBF));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo((ushort)30));
                Assert.That(
                    BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(3, 2)),
                    Is.EqualTo((ushort)GeneralInformationSubcommandType.DisplayPopupContextMenu)
                );
                Assert.That(data[5], Is.EqualTo(0x00));
                Assert.That(data[6], Is.EqualTo(0x01));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(7, 4)), Is.EqualTo((uint)0x00000009));
                Assert.That(data[11], Is.EqualTo(0x03));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(12, 2)), Is.EqualTo((ushort)1));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(14, 2)), Is.EqualTo((ushort)6123));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(16, 2)), Is.EqualTo((ushort)0x0000));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(18, 2)), Is.EqualTo((ushort)2));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(20, 2)), Is.EqualTo((ushort)6103));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(22, 2)), Is.EqualTo((ushort)0x0000));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(24, 2)), Is.EqualTo((ushort)3));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(26, 2)), Is.EqualTo((ushort)6104));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(28, 2)), Is.EqualTo((ushort)0x0001));
            }
        );
    }

    private static byte[] Write(GeneralInformationPacket packet)
    {
        var writer = new SpanWriter(64, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
