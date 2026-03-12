using System.Buffers.Binary;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Network.Packets;

public class PlayerStatusPacketTests
{
    [Test]
    public void TryParse_ShouldPopulateFields()
    {
        var sourceMobile = new UOMobileEntity
        {
            Id = (Serial)0x00000002,
            Name = "Tommy",
            Hits = 50,
            MaxHits = 100
        };
        var sourcePacket = new PlayerStatusPacket(sourceMobile);
        var payload = Write(sourcePacket);
        var packet = new PlayerStatusPacket();

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.Serial.Value, Is.EqualTo(0x00000002u));
                Assert.That(packet.Name, Is.EqualTo("Tommy"));
                Assert.That(packet.CurrentHits, Is.EqualTo(50));
                Assert.That(packet.MaxHits, Is.EqualTo(100));
                Assert.That(packet.CanBeRenamed, Is.False);
                Assert.That(packet.Version, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeCompactPlayerStatus()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000002,
            Name = "Tommy",
            Hits = 50,
            MaxHits = 100
        };
        var packet = new PlayerStatusPacket(mobile);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(43));
                Assert.That(data[0], Is.EqualTo(0x11));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo((ushort)43));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(3, 4)), Is.EqualTo(0x00000002u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(37, 2)), Is.EqualTo((ushort)50));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(39, 2)), Is.EqualTo((ushort)100));
                Assert.That(data[41], Is.EqualTo(0x00));
                Assert.That(data[42], Is.EqualTo(0x00));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeEffectiveStats_ForVersionOnePacket()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000003,
            Name = "Effective Tommy",
            BaseStats = new()
            {
                Strength = 60,
                Dexterity = 50,
                Intelligence = 40
            },
            Resources = new()
            {
                Hits = 55,
                MaxHits = 70,
                Stamina = 45,
                MaxStamina = 50,
                Mana = 35,
                MaxMana = 40
            },
            EquipmentModifiers = new()
            {
                StrengthBonus = 5,
                DexterityBonus = 2,
                IntelligenceBonus = 1
            },
            RuntimeModifiers = new()
            {
                StrengthBonus = -1,
                DexterityBonus = 3,
                IntelligenceBonus = 4
            }
        };
        var packet = new PlayerStatusPacket(mobile, version: 1);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(44, 2)), Is.EqualTo((ushort)64));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(46, 2)), Is.EqualTo((ushort)55));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(48, 2)), Is.EqualTo((ushort)45));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(50, 2)), Is.EqualTo((ushort)45));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(52, 2)), Is.EqualTo((ushort)50));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(54, 2)), Is.EqualTo((ushort)35));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(56, 2)), Is.EqualTo((ushort)40));
            }
        );
    }

    private static byte[] Write(PlayerStatusPacket packet)
    {
        var writer = new SpanWriter(64, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
