using System.Buffers.Binary;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class SendSkillsPacketTests
{
    [SetUp]
    public void SetUp()
        => SkillInfo.Table =
        [
            new(
                0,
                "Alchemy",
                0,
                0,
                100,
                "Alchemist",
                0,
                0,
                0,
                1,
                "Alchemy",
                Stat.Intelligence,
                Stat.Intelligence
            ),
            new(
                25,
                "Magery",
                0,
                0,
                100,
                "Wizard",
                0,
                0,
                0,
                1,
                "Magery",
                Stat.Intelligence,
                Stat.Intelligence
            )
        ];

    [Test]
    public void Write_ShouldSerializeFullSkillListWithLocks()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000002,
            Name = "Tommy"
        };
        mobile.InitializeSkills();
        mobile.SetSkill(UOSkillName.Alchemy, 500, cap: 900, lockState: UOSkillLock.Locked);
        mobile.SetSkill(UOSkillName.Magery, 725, 700, 1000, UOSkillLock.Down);
        var packet = new SkillListPacket(mobile);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data[0], Is.EqualTo(0x3A));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo((ushort)20));
                Assert.That(data[3], Is.EqualTo((byte)SendSkillResponseType.FullSkillList));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2)), Is.EqualTo((ushort)1));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6, 2)), Is.EqualTo((ushort)500));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8, 2)), Is.EqualTo((ushort)500));
                Assert.That(data[10], Is.EqualTo((byte)UOSkillLock.Locked));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(11, 2)), Is.EqualTo((ushort)26));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(13, 2)), Is.EqualTo((ushort)725));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(15, 2)), Is.EqualTo((ushort)700));
                Assert.That(data[17], Is.EqualTo((byte)UOSkillLock.Down));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(18, 2)), Is.EqualTo((ushort)0));
            }
        );
    }

    private static byte[] Write(SkillListPacket packet)
    {
        var writer = new SpanWriter(64, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
