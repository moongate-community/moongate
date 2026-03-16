using Moongate.Persistence.Data.Internal;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Persistence;

public class SnapshotMapperTests
{
    [Test]
    public void ToBulletinBoardMessageSnapshot_ShouldPreserveBodyAndMetadata()
    {
        var entity = new BulletinBoardMessageEntity
        {
            MessageId = (Serial)0x40000091u,
            BoardId = (Serial)0x40000055u,
            ParentId = (Serial)0x40000011u,
            OwnerCharacterId = (Serial)0x00000077u,
            Author = "The Poster",
            Subject = "Test Subject",
            PostedAtUtc = new(2026, 3, 13, 12, 0, 0, DateTimeKind.Utc)
        };
        entity.BodyLines.AddRange(["line one", "line two"]);

        var snapshot = SnapshotMapper.ToBulletinBoardMessageSnapshot(entity);
        var restored = SnapshotMapper.ToBulletinBoardMessageEntity(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored.MessageId, Is.EqualTo(entity.MessageId));
                Assert.That(restored.BoardId, Is.EqualTo(entity.BoardId));
                Assert.That(restored.ParentId, Is.EqualTo(entity.ParentId));
                Assert.That(restored.OwnerCharacterId, Is.EqualTo(entity.OwnerCharacterId));
                Assert.That(restored.Author, Is.EqualTo("The Poster"));
                Assert.That(restored.Subject, Is.EqualTo("Test Subject"));
                Assert.That(restored.PostedAtUtc, Is.EqualTo(entity.PostedAtUtc));
                Assert.That(restored.BodyLines, Is.EqualTo(new[] { "line one", "line two" }));
            }
        );
    }

    [Test]
    public void ToItemSnapshot_ShouldPreserveCombatStatsAndModifiers()
    {
        var entity = new UOItemEntity
        {
            Id = (Serial)0x40000010u,
            Name = "typed-item",
            Location = new(10, 20, 0),
            ItemId = 0x13B9,
            CombatStats = new()
            {
                MinStrength = 40,
                DamageMin = 11,
                DamageMax = 13,
                Defense = 15,
                AttackSpeed = 30,
                RangeMin = 1,
                RangeMax = 2,
                MaxDurability = 45,
                CurrentDurability = 40
            },
            Modifiers = new()
            {
                StrengthBonus = 5,
                PhysicalResist = 12,
                FireResist = 8,
                HitChanceIncrease = 10,
                DefenseChanceIncrease = 7,
                Luck = 100,
                SpellChanneling = 1,
                UsesRemaining = 25
            }
        };

        var snapshot = SnapshotMapper.ToItemSnapshot(entity);
        var restored = SnapshotMapper.ToItemEntity(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored.CombatStats, Is.Not.Null);
                Assert.That(restored.CombatStats!.MinStrength, Is.EqualTo(40));
                Assert.That(restored.CombatStats.DamageMin, Is.EqualTo(11));
                Assert.That(restored.CombatStats.DamageMax, Is.EqualTo(13));
                Assert.That(restored.CombatStats.Defense, Is.EqualTo(15));
                Assert.That(restored.CombatStats.AttackSpeed, Is.EqualTo(30));
                Assert.That(restored.CombatStats.RangeMin, Is.EqualTo(1));
                Assert.That(restored.CombatStats.RangeMax, Is.EqualTo(2));
                Assert.That(restored.CombatStats.MaxDurability, Is.EqualTo(45));
                Assert.That(restored.CombatStats.CurrentDurability, Is.EqualTo(40));

                Assert.That(restored.Modifiers, Is.Not.Null);
                Assert.That(restored.Modifiers!.StrengthBonus, Is.EqualTo(5));
                Assert.That(restored.Modifiers.PhysicalResist, Is.EqualTo(12));
                Assert.That(restored.Modifiers.FireResist, Is.EqualTo(8));
                Assert.That(restored.Modifiers.HitChanceIncrease, Is.EqualTo(10));
                Assert.That(restored.Modifiers.DefenseChanceIncrease, Is.EqualTo(7));
                Assert.That(restored.Modifiers.Luck, Is.EqualTo(100));
                Assert.That(restored.Modifiers.SpellChanneling, Is.EqualTo(1));
                Assert.That(restored.Modifiers.UsesRemaining, Is.EqualTo(25));
            }
        );
    }

    [Test]
    public void ToMobileSnapshot_ShouldPreserveCustomProperties()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x102u,
            Name = "props",
            Location = new(0, 0, 0)
        };
        entity.SetCustomProperty(
            "test_key",
            new()
            {
                Type = ItemCustomPropertyType.Integer,
                IntegerValue = 42
            }
        );

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);
        var restored = SnapshotMapper.ToMobileEntity(snapshot);

        Assert.That(restored.CustomProperties.Count, Is.EqualTo(1));
        Assert.That(restored.CustomProperties["test_key"].IntegerValue, Is.EqualTo(42));
    }

    [Test]
    public void ToMobileSnapshot_ShouldPreserveEquippedItems_InLayerOrder()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x100u,
            Name = "test",
            Location = new(100, 200, 0),
            EquippedItemIds =
            {
                [ItemLayerType.Shirt] = (Serial)0x202u,
                [ItemLayerType.OneHanded] = (Serial)0x200u,
                [ItemLayerType.Shoes] = (Serial)0x201u
            }
        };

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);
        var restored = SnapshotMapper.ToMobileEntity(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.EquippedLayers.Length, Is.EqualTo(3));
                Assert.That(snapshot.EquippedItemIds.Length, Is.EqualTo(3));

                // Verify ordering by layer key (ascending)
                for (var i = 1; i < snapshot.EquippedLayers.Length; i++)
                {
                    Assert.That(snapshot.EquippedLayers[i], Is.GreaterThanOrEqualTo(snapshot.EquippedLayers[i - 1]));
                }

                // Verify round-trip
                Assert.That(restored.EquippedItemIds.Count, Is.EqualTo(3));
                Assert.That(restored.EquippedItemIds[ItemLayerType.OneHanded], Is.EqualTo((Serial)0x200u));
                Assert.That(restored.EquippedItemIds[ItemLayerType.Shoes], Is.EqualTo((Serial)0x201u));
                Assert.That(restored.EquippedItemIds[ItemLayerType.Shirt], Is.EqualTo((Serial)0x202u));
            }
        );
    }

    [Test]
    public void ToMobileSnapshot_ShouldPreserveLifeStatusFields()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x150u,
            Name = "status",
            Location = new(10, 20, 0),
            Hunger = 20,
            Thirst = 19,
            Fame = 1500,
            Karma = -1200,
            Kills = 3
        };

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);
        var restored = SnapshotMapper.ToMobileEntity(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.Hunger, Is.EqualTo(20));
                Assert.That(snapshot.Thirst, Is.EqualTo(19));
                Assert.That(snapshot.Fame, Is.EqualTo(1500));
                Assert.That(snapshot.Karma, Is.EqualTo(-1200));
                Assert.That(snapshot.Kills, Is.EqualTo(3));

                Assert.That(restored.Hunger, Is.EqualTo(20));
                Assert.That(restored.Thirst, Is.EqualTo(19));
                Assert.That(restored.Fame, Is.EqualTo(1500));
                Assert.That(restored.Karma, Is.EqualTo(-1200));
                Assert.That(restored.Kills, Is.EqualTo(3));
            }
        );
    }

    [Test]
    public void ToMobileSnapshot_ShouldPreserveSkills()
    {
        SkillInfo.Table =
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
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x111u,
            Name = "skilled-mobile"
        };
        entity.SetSkill(UOSkillName.Alchemy, 500, cap: 900, lockState: UOSkillLock.Locked);
        entity.SetSkill(UOSkillName.Magery, 725, 700, 1000, UOSkillLock.Down);

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);
        var restored = SnapshotMapper.ToMobileEntity(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.Skills, Has.Length.EqualTo(2));
                Assert.That(restored.Skills, Has.Count.EqualTo(2));
                Assert.That(restored.Skills[UOSkillName.Alchemy].Value, Is.EqualTo(500));
                Assert.That(restored.Skills[UOSkillName.Alchemy].Base, Is.EqualTo(500));
                Assert.That(restored.Skills[UOSkillName.Alchemy].Cap, Is.EqualTo(900));
                Assert.That(restored.Skills[UOSkillName.Alchemy].Lock, Is.EqualTo(UOSkillLock.Locked));
                Assert.That(restored.Skills[UOSkillName.Magery].Value, Is.EqualTo(725));
                Assert.That(restored.Skills[UOSkillName.Magery].Base, Is.EqualTo(700));
                Assert.That(restored.Skills[UOSkillName.Magery].Cap, Is.EqualTo(1000));
                Assert.That(restored.Skills[UOSkillName.Magery].Lock, Is.EqualTo(UOSkillLock.Down));
            }
        );
    }

    [Test]
    public void ToMobileSnapshot_ShouldPreserveTypedBaseStateAndModifiers()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x110u,
            Name = "typed-mobile",
            Location = new(10, 20, 0),
            BaseStats = new()
            {
                Strength = 60,
                Dexterity = 50,
                Intelligence = 40
            },
            BaseResistances = new()
            {
                Physical = 5,
                Fire = 15,
                Cold = 11,
                Poison = 9,
                Energy = 13
            },
            Resources = new()
            {
                Hits = 60,
                MaxHits = 70,
                Mana = 40,
                MaxMana = 50,
                Stamina = 50,
                MaxStamina = 60
            },
            BaseLuck = 42,
            StatCap = 250,
            Followers = 2,
            FollowersMax = 5,
            Weight = 33,
            MaxWeight = 400,
            MinWeaponDamage = 11,
            MaxWeaponDamage = 15,
            Tithing = 777,
            EquipmentModifiers = new()
            {
                StrengthBonus = 5,
                FireResist = 3,
                Luck = 10,
                HitChanceIncrease = 8
            },
            RuntimeModifiers = new()
            {
                StrengthBonus = -2,
                FireResist = 4,
                Luck = 20,
                DefenseChanceIncrease = 7
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

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);
        var restored = SnapshotMapper.ToMobileEntity(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored.BaseStats.Strength, Is.EqualTo(60));
                Assert.That(restored.BaseStats.Dexterity, Is.EqualTo(50));
                Assert.That(restored.BaseStats.Intelligence, Is.EqualTo(40));
                Assert.That(restored.BaseResistances.Physical, Is.EqualTo(5));
                Assert.That(restored.BaseResistances.Fire, Is.EqualTo(15));
                Assert.That(restored.BaseResistances.Cold, Is.EqualTo(11));
                Assert.That(restored.BaseResistances.Poison, Is.EqualTo(9));
                Assert.That(restored.BaseResistances.Energy, Is.EqualTo(13));
                Assert.That(restored.Resources.Hits, Is.EqualTo(60));
                Assert.That(restored.Resources.MaxHits, Is.EqualTo(70));
                Assert.That(restored.Resources.Mana, Is.EqualTo(40));
                Assert.That(restored.Resources.MaxMana, Is.EqualTo(50));
                Assert.That(restored.Resources.Stamina, Is.EqualTo(50));
                Assert.That(restored.Resources.MaxStamina, Is.EqualTo(60));
                Assert.That(restored.BaseLuck, Is.EqualTo(42));
                Assert.That(restored.StatCap, Is.EqualTo(250));
                Assert.That(restored.Followers, Is.EqualTo(2));
                Assert.That(restored.FollowersMax, Is.EqualTo(5));
                Assert.That(restored.Weight, Is.EqualTo(33));
                Assert.That(restored.MaxWeight, Is.EqualTo(400));
                Assert.That(restored.MinWeaponDamage, Is.EqualTo(11));
                Assert.That(restored.MaxWeaponDamage, Is.EqualTo(15));
                Assert.That(restored.Tithing, Is.EqualTo(777));
                Assert.That(restored.EquipmentModifiers, Is.Not.Null);
                Assert.That(restored.EquipmentModifiers!.StrengthBonus, Is.EqualTo(5));
                Assert.That(restored.EquipmentModifiers.FireResist, Is.EqualTo(3));
                Assert.That(restored.EquipmentModifiers.Luck, Is.EqualTo(10));
                Assert.That(restored.EquipmentModifiers.HitChanceIncrease, Is.EqualTo(8));
                Assert.That(restored.RuntimeModifiers, Is.Not.Null);
                Assert.That(restored.RuntimeModifiers!.StrengthBonus, Is.EqualTo(-2));
                Assert.That(restored.RuntimeModifiers.FireResist, Is.EqualTo(4));
                Assert.That(restored.RuntimeModifiers.Luck, Is.EqualTo(20));
                Assert.That(restored.RuntimeModifiers.DefenseChanceIncrease, Is.EqualTo(7));
                Assert.That(restored.ModifierCaps.PhysicalResist, Is.EqualTo(70));
                Assert.That(restored.ModifierCaps.FireResist, Is.EqualTo(71));
                Assert.That(restored.ModifierCaps.DefenseChanceIncrease, Is.EqualTo(45));
                Assert.That(restored.Strength, Is.EqualTo(60));
                Assert.That(restored.FireResistance, Is.EqualTo(15));
                Assert.That(restored.Luck, Is.EqualTo(42));
                Assert.That(restored.EffectiveStrength, Is.EqualTo(63));
                Assert.That(restored.EffectiveFireResistance, Is.EqualTo(22));
                Assert.That(restored.EffectiveLuck, Is.EqualTo(72));
            }
        );
    }

    [Test]
    public void ToMobileSnapshot_WithEmptyEquipped_ShouldProduceEmptyArrays()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x101u,
            Name = "empty",
            Location = new(0, 0, 0)
        };

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.EquippedLayers, Is.Empty);
                Assert.That(snapshot.EquippedItemIds, Is.Empty);
            }
        );
    }
}
