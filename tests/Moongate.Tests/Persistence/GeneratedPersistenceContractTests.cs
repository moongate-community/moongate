using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Persistence;

public sealed class GeneratedPersistenceContractTests
{
    [Test]
    public void BulletinBoardPersistence_ShouldPreserveBodyAndMetadata()
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

        var snapshot = BulletinBoardMessageEntityPersistence.ToSnapshot(entity);
        var restored = BulletinBoardMessageEntityPersistence.FromSnapshot(snapshot);

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
    public void ItemPersistence_ShouldPreserveCombatStatsModifiersAndCustomProperties()
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
        entity.SetCustomProperty(
            "crafted_by",
            new()
            {
                Type = ItemCustomPropertyType.String,
                StringValue = "The Blacksmith"
            }
        );

        var snapshot = UOItemEntityPersistence.ToSnapshot(entity);
        var restored = UOItemEntityPersistence.FromSnapshot(snapshot);

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
                Assert.That(restored.CustomProperties["crafted_by"].StringValue, Is.EqualTo("The Blacksmith"));
            }
        );
    }

    [Test]
    public void MobilePersistence_ShouldPreserveCustomProperties()
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

        var snapshot = UOMobileEntityPersistence.ToSnapshot(entity);
        var restored = UOMobileEntityPersistence.FromSnapshot(snapshot);

        Assert.That(restored.CustomProperties.Count, Is.EqualTo(1));
        Assert.That(restored.CustomProperties["test_key"].IntegerValue, Is.EqualTo(42));
    }

    [Test]
    public void MobilePersistence_ShouldPreserveSounds()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x102u,
            Name = "sounds",
            Location = new(0, 0, 0),
            Sounds =
            {
                [MobileSoundType.StartAttack] = 0x0135,
                [MobileSoundType.Attack] = 0x023B,
                [MobileSoundType.Defend] = 0x0140
            }
        };

        var snapshot = UOMobileEntityPersistence.ToSnapshot(entity);
        var restored = UOMobileEntityPersistence.FromSnapshot(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored.Sounds, Has.Count.EqualTo(3));
                Assert.That(restored.Sounds[MobileSoundType.StartAttack], Is.EqualTo(0x0135));
                Assert.That(restored.Sounds[MobileSoundType.Attack], Is.EqualTo(0x023B));
                Assert.That(restored.Sounds[MobileSoundType.Defend], Is.EqualTo(0x0140));
            }
        );
    }

    [Test]
    public void MobilePersistence_ShouldPreserveEquippedItems_InLayerOrder()
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

        var snapshot = UOMobileEntityPersistence.ToSnapshot(entity);
        var restored = UOMobileEntityPersistence.FromSnapshot(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.EquippedItems.Length, Is.EqualTo(3));

                for (var i = 1; i < snapshot.EquippedItems.Length; i++)
                {
                    Assert.That((int)snapshot.EquippedItems[i].Layer, Is.GreaterThanOrEqualTo((int)snapshot.EquippedItems[i - 1].Layer));
                }

                Assert.That(restored.EquippedItemIds.Count, Is.EqualTo(3));
                Assert.That(restored.EquippedItemIds[ItemLayerType.OneHanded], Is.EqualTo((Serial)0x200u));
                Assert.That(restored.EquippedItemIds[ItemLayerType.Shoes], Is.EqualTo((Serial)0x201u));
                Assert.That(restored.EquippedItemIds[ItemLayerType.Shirt], Is.EqualTo((Serial)0x202u));
            }
        );
    }

    [Test]
    public void MobilePersistence_ShouldPreserveLifeStatusFields()
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

        var snapshot = UOMobileEntityPersistence.ToSnapshot(entity);
        var restored = UOMobileEntityPersistence.FromSnapshot(snapshot);

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
    public void MobilePersistence_ShouldPreserveSkills()
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

        var snapshot = UOMobileEntityPersistence.ToSnapshot(entity);
        var restored = UOMobileEntityPersistence.FromSnapshot(snapshot);

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
    public void MobilePersistence_ShouldPreserveTypedBaseStateAndModifiers()
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
                Cold = 10,
                Poison = 20,
                Energy = 25
            },
            Resources = new()
            {
                Hits = 75,
                Mana = 40,
                Stamina = 55,
                MaxHits = 80,
                MaxMana = 45,
                MaxStamina = 60
            },
            EquipmentModifiers = new()
            {
                StrengthBonus = 3,
                DexterityBonus = 2,
                IntelligenceBonus = 1,
                PhysicalResist = 4,
                FireResist = 5,
                ColdResist = 6,
                PoisonResist = 7,
                EnergyResist = 8,
                HitChanceIncrease = 9,
                DefenseChanceIncrease = 10,
                DamageIncrease = 11,
                SwingSpeedIncrease = 12,
                SpellDamageIncrease = 13,
                FasterCasting = 1,
                FasterCastRecovery = 2,
                LowerManaCost = 3,
                LowerReagentCost = 4,
                Luck = 55,
                SpellChanneling = 1
            },
            RuntimeModifiers = new()
            {
                StrengthBonus = 1,
                DexterityBonus = 1,
                IntelligenceBonus = 1
            },
            ModifierCaps = new()
            {
                PhysicalResist = 70,
                FireResist = 70,
                ColdResist = 70,
                PoisonResist = 70,
                EnergyResist = 70,
                DefenseChanceIncrease = 45
            },
            BaseLuck = 120
        };

        var snapshot = UOMobileEntityPersistence.ToSnapshot(entity);
        var restored = UOMobileEntityPersistence.FromSnapshot(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored.BaseStats.Strength, Is.EqualTo(60));
                Assert.That(restored.BaseStats.Dexterity, Is.EqualTo(50));
                Assert.That(restored.BaseStats.Intelligence, Is.EqualTo(40));
                Assert.That(restored.BaseResistances.Physical, Is.EqualTo(5));
                Assert.That(restored.BaseResistances.Fire, Is.EqualTo(15));
                Assert.That(restored.BaseResistances.Cold, Is.EqualTo(10));
                Assert.That(restored.BaseResistances.Poison, Is.EqualTo(20));
                Assert.That(restored.BaseResistances.Energy, Is.EqualTo(25));
                Assert.That(restored.Resources.Hits, Is.EqualTo(75));
                Assert.That(restored.Resources.MaxHits, Is.EqualTo(80));
                Assert.That(restored.Resources.Mana, Is.EqualTo(40));
                Assert.That(restored.Resources.MaxMana, Is.EqualTo(45));
                Assert.That(restored.Resources.Stamina, Is.EqualTo(55));
                Assert.That(restored.Resources.MaxStamina, Is.EqualTo(60));
                Assert.That(restored.EquipmentModifiers, Is.Not.Null);
                Assert.That(restored.EquipmentModifiers!.StrengthBonus, Is.EqualTo(3));
                Assert.That(restored.EquipmentModifiers.Luck, Is.EqualTo(55));
                Assert.That(restored.RuntimeModifiers, Is.Not.Null);
                Assert.That(restored.RuntimeModifiers!.StrengthBonus, Is.EqualTo(1));
                Assert.That(restored.ModifierCaps.DefenseChanceIncrease, Is.EqualTo(45));
                Assert.That(restored.BaseLuck, Is.EqualTo(120));
            }
        );
    }
}
