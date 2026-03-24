using System.Buffers.Binary;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

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
            MaxHits = 100,
            Weight = 12,
            MaxWeight = 400,
            StatCap = 225,
            Followers = 2,
            FollowersMax = 5
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
                Assert.That(packet.Version, Is.EqualTo(PlayerStatusPacket.ModernVersion));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeModernEffectiveStatusValues()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000003,
            Name = "Effective Tommy",
            IsPlayer = true,
            BaseStats = new()
            {
                Strength = 60,
                Dexterity = 50,
                Intelligence = 40
            },
            BaseResistances = new()
            {
                Physical = 5,
                Fire = 10,
                Cold = 15,
                Poison = 20,
                Energy = 25
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
            BaseLuck = 100,
            StatCap = 260,
            Followers = 3,
            FollowersMax = 5,
            MinWeaponDamage = 11,
            MaxWeaponDamage = 15,
            Tithing = 777,
            RaceIndex = 1,
            EquipmentModifiers = new()
            {
                StrengthBonus = 5,
                DexterityBonus = 2,
                IntelligenceBonus = 1,
                PhysicalResist = 2,
                FireResist = 3,
                HitChanceIncrease = 8,
                DefenseChanceIncrease = 7,
                DamageIncrease = 12,
                SwingSpeedIncrease = 9,
                SpellDamageIncrease = 11,
                FasterCasting = 2,
                FasterCastRecovery = 3,
                LowerManaCost = 4,
                LowerReagentCost = 5,
                Luck = 20
            },
            RuntimeModifiers = new()
            {
                StrengthBonus = -1,
                DexterityBonus = 3,
                IntelligenceBonus = 4,
                PhysicalResist = 1,
                FireResist = 4,
                DefenseChanceIncrease = 2,
                Luck = 30
            },
            ModifierCaps = new()
            {
                PhysicalResist = 70,
                FireResist = 71,
                ColdResist = 72,
                PoisonResist = 73,
                EnergyResist = 74,
                DefenseChanceIncrease = 45
            }
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000030,
            ItemId = 0x0E75,
            Weight = 2
        };
        var gold = new UOItemEntity
        {
            Id = (Serial)0x40000031,
            ItemId = 0x0EED,
            Weight = 0,
            Amount = 1000
        };
        var quiver = new UOItemEntity
        {
            Id = (Serial)0x40000032,
            ItemId = 0x2FB7,
            Weight = 2,
            IsQuiver = true,
            QuiverWeightReduction = 30
        };
        var arrows = new UOItemEntity
        {
            Id = (Serial)0x40000033,
            ItemId = 0x0F3F,
            Weight = 1,
            Amount = 10
        };
        backpack.AddItem(gold, new(1, 1));
        quiver.AddItem(arrows, new(2, 2));
        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        mobile.BackpackId = backpack.Id;
        mobile.AddEquippedItem(ItemLayerType.Cloak, quiver);
        var packet = new PlayerStatusPacket(mobile);

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
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(58, 4)), Is.EqualTo((uint)1000));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(62, 2)), Is.EqualTo((ushort)8));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(64, 2)), Is.EqualTo((ushort)22));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(66, 2)), Is.EqualTo((ushort)264));
                Assert.That(data[68], Is.EqualTo((byte)2));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(69, 2)), Is.EqualTo((ushort)260));
                Assert.That(data[71], Is.EqualTo((byte)3));
                Assert.That(data[72], Is.EqualTo((byte)5));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(73, 2)), Is.EqualTo((ushort)17));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(75, 2)), Is.EqualTo((ushort)15));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(77, 2)), Is.EqualTo((ushort)20));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(79, 2)), Is.EqualTo((ushort)25));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(81, 2)), Is.EqualTo((ushort)150));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(83, 2)), Is.EqualTo((ushort)1));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(85, 2)), Is.EqualTo((ushort)4));
                Assert.That(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(87, 4)), Is.EqualTo(777));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(91, 2)), Is.EqualTo((ushort)70));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(103, 2)), Is.EqualTo((ushort)45));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(105, 2)), Is.EqualTo((ushort)8));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(107, 2)), Is.EqualTo((ushort)9));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(109, 2)), Is.EqualTo((ushort)12));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(111, 2)), Is.EqualTo((ushort)5));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(113, 2)), Is.EqualTo((ushort)11));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(115, 2)), Is.EqualTo((ushort)3));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(117, 2)), Is.EqualTo((ushort)2));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(119, 2)), Is.EqualTo((ushort)4));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeModernPlayerStatusHeader()
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
                Assert.That(data.Length, Is.EqualTo(121));
                Assert.That(data[0], Is.EqualTo(0x11));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo((ushort)121));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(3, 4)), Is.EqualTo(0x00000002u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(37, 2)), Is.EqualTo((ushort)50));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(39, 2)), Is.EqualTo((ushort)100));
                Assert.That(data[41], Is.EqualTo(0x00));
                Assert.That(data[42], Is.EqualTo(PlayerStatusPacket.ModernVersion));
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
