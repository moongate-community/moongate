using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.UO.Data.Persistence.Entities;

public class UOMobileEntityTests
{
    [Test]
    public void AddEquippedItem_WithEntity_ShouldTrackSlotAndUpdateItemOwnership()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000077
        };

        var item = new UOItemEntity
        {
            Id = (Serial)0x40000077,
            ParentContainerId = (Serial)0x40000050,
            ContainerPosition = new(10, 20)
        };

        mobile.AddEquippedItem(ItemLayerType.Shirt, item);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.EquippedItemIds[ItemLayerType.Shirt], Is.EqualTo(item.Id));
                Assert.That(item.ParentContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(item.ContainerPosition.X, Is.EqualTo(0));
                Assert.That(item.ContainerPosition.Y, Is.EqualTo(0));
                Assert.That(item.EquippedMobileId, Is.EqualTo(mobile.Id));
                Assert.That(item.EquippedLayer, Is.EqualTo(ItemLayerType.Shirt));
            }
        );
    }

    [Test]
    public void CombatState_ShouldDefaultToNoCombatantAndEmptyAggressorLists()
    {
        var mobile = new UOMobileEntity();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.CombatantId, Is.EqualTo(Serial.Zero));
                Assert.That(mobile.Warmode, Is.False);
                Assert.That(mobile.NextCombatAtUtc, Is.Null);
                Assert.That(mobile.LastCombatAtUtc, Is.Null);
                Assert.That(mobile.Aggressors, Is.Empty);
                Assert.That(mobile.Aggressed, Is.Empty);
            }
        );
    }

    [Test]
    public void WarmodeAlias_ShouldMapToIsWarMode()
    {
        var mobile = new UOMobileEntity
        {
            Warmode = true
        };

        Assert.That(mobile.IsWarMode, Is.True);

        mobile.IsWarMode = false;

        Assert.That(mobile.Warmode, Is.False);
    }

    [Test]
    public void RefreshAggressor_ShouldAddAndUpdateExistingEntries()
    {
        var now = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);
        var later = now.AddSeconds(30);
        var attackerId = (Serial)0x00000010;
        var defenderId = (Serial)0x00000020;
        var mobile = new UOMobileEntity();

        mobile.RefreshAggressor(attackerId, defenderId, now);
        mobile.RefreshAggressor(attackerId, defenderId, later, isCriminal: true);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Aggressors, Has.Count.EqualTo(1));
                Assert.That(mobile.Aggressed, Has.Count.EqualTo(1));
                Assert.That(mobile.Aggressors[0].LastCombatAtUtc, Is.EqualTo(later));
                Assert.That(mobile.Aggressors[0].IsCriminal, Is.True);
                Assert.That(mobile.Aggressed[0].LastCombatAtUtc, Is.EqualTo(later));
            }
        );
    }

    [Test]
    public void ExpireAggressors_ShouldRemoveTimedOutEntries()
    {
        var now = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);
        var mobile = new UOMobileEntity();

        mobile.Aggressors.Add(new((Serial)0x10, (Serial)0x20, now.AddMinutes(-3), false, false));
        mobile.Aggressors.Add(new((Serial)0x11, (Serial)0x21, now.AddSeconds(-30), false, false));
        mobile.Aggressed.Add(new((Serial)0x12, (Serial)0x22, now.AddMinutes(-4), false, false));
        mobile.Aggressed.Add(new((Serial)0x13, (Serial)0x23, now.AddSeconds(-10), false, false));

        mobile.ExpireAggressors(now, TimeSpan.FromMinutes(2));

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Aggressors, Has.Count.EqualTo(1));
                Assert.That(mobile.Aggressors[0].AttackerId, Is.EqualTo((Serial)0x11));
                Assert.That(mobile.Aggressed, Has.Count.EqualTo(1));
                Assert.That(mobile.Aggressed[0].AttackerId, Is.EqualTo((Serial)0x13));
            }
        );
    }

    [Test]
    public void ApplyAndRemoveRuntimeModifier_ShouldUpdateEffectiveValues()
    {
        var mobile = new UOMobileEntity
        {
            BaseStats = new()
            {
                Strength = 60
            },
            BaseResistances = new()
            {
                Fire = 10
            },
            BaseLuck = 100
        };
        var delta = new MobileModifierDelta
        {
            StrengthBonus = 5,
            FireResist = 3,
            Luck = 20
        };

        mobile.ApplyRuntimeModifier(delta);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.RuntimeModifiers, Is.Not.Null);
                Assert.That(mobile.EffectiveStrength, Is.EqualTo(65));
                Assert.That(mobile.EffectiveFireResistance, Is.EqualTo(13));
                Assert.That(mobile.EffectiveLuck, Is.EqualTo(120));
            }
        );

        mobile.RemoveRuntimeModifier(delta);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.RuntimeModifiers, Is.Null);
                Assert.That(mobile.EffectiveStrength, Is.EqualTo(60));
                Assert.That(mobile.EffectiveFireResistance, Is.EqualTo(10));
                Assert.That(mobile.EffectiveLuck, Is.EqualTo(100));
            }
        );
    }

    [Test]
    public void BodyProperty_WhenSet_ShouldReturnExplicitBody()
    {
        var mobile = new UOMobileEntity();

        mobile.Body = 0x0191;

        Assert.That((int)mobile.Body, Is.EqualTo(0x0191));
    }

    [Test]
    public void CustomProperties_ShouldStoreTypedValues()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001003
        };

        mobile.SetCustomInteger("owner_id", 1234);
        mobile.SetCustomBoolean("is_boss", true);
        mobile.SetCustomDouble("scale", 1.5d);
        mobile.SetCustomString("title_suffix", "the brave");

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.TryGetCustomInteger("owner_id", out var ownerId), Is.True);
                Assert.That(ownerId, Is.EqualTo(1234));
                Assert.That(mobile.TryGetCustomBoolean("is_boss", out var isBoss), Is.True);
                Assert.That(isBoss, Is.True);
                Assert.That(mobile.TryGetCustomDouble("scale", out var scale), Is.True);
                Assert.That(scale, Is.EqualTo(1.5d));
                Assert.That(mobile.TryGetCustomString("title_suffix", out var titleSuffix), Is.True);
                Assert.That(titleSuffix, Is.EqualTo("the brave"));
                Assert.That(mobile.CustomProperties, Has.Count.EqualTo(4));
            }
        );
    }

    [Test]
    public void EffectiveProperties_ShouldCombineBaseEquipmentAndRuntimeModifiers()
    {
        var mobile = new UOMobileEntity
        {
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
            BaseLuck = 100,
            StatCap = 250,
            Followers = 2,
            FollowersMax = 5,
            EquipmentModifiers = new()
            {
                StrengthBonus = 5,
                FireResist = 3,
                PhysicalResist = 2,
                Luck = 20,
                HitChanceIncrease = 8,
                DefenseChanceIncrease = 7,
                DamageIncrease = 12,
                SwingSpeedIncrease = 9,
                SpellDamageIncrease = 11,
                FasterCastRecovery = 3,
                FasterCasting = 2,
                LowerManaCost = 4,
                LowerReagentCost = 5
            },
            RuntimeModifiers = new()
            {
                StrengthBonus = -2,
                FireResist = 4,
                PhysicalResist = 1,
                Luck = 30,
                DefenseChanceIncrease = 2
            },
            ModifierCaps = new()
            {
                PhysicalResist = 70,
                DefenseChanceIncrease = 45
            }
        };

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.EffectiveStrength, Is.EqualTo(63));
                Assert.That(mobile.EffectiveDexterity, Is.EqualTo(50));
                Assert.That(mobile.EffectiveIntelligence, Is.EqualTo(40));
                Assert.That(mobile.EffectivePhysicalResistance, Is.EqualTo(8));
                Assert.That(mobile.EffectiveFireResistance, Is.EqualTo(17));
                Assert.That(mobile.EffectiveColdResistance, Is.EqualTo(15));
                Assert.That(mobile.EffectivePoisonResistance, Is.EqualTo(20));
                Assert.That(mobile.EffectiveEnergyResistance, Is.EqualTo(25));
                Assert.That(mobile.EffectiveLuck, Is.EqualTo(150));
                Assert.That(mobile.EffectiveHitChanceIncrease, Is.EqualTo(8));
                Assert.That(mobile.EffectiveDefenseChanceIncrease, Is.EqualTo(9));
                Assert.That(mobile.EffectiveDamageIncrease, Is.EqualTo(12));
                Assert.That(mobile.EffectiveSwingSpeedIncrease, Is.EqualTo(9));
                Assert.That(mobile.EffectiveSpellDamageIncrease, Is.EqualTo(11));
                Assert.That(mobile.EffectiveFasterCastRecovery, Is.EqualTo(3));
                Assert.That(mobile.EffectiveFasterCasting, Is.EqualTo(2));
                Assert.That(mobile.EffectiveLowerManaCost, Is.EqualTo(4));
                Assert.That(mobile.EffectiveLowerReagentCost, Is.EqualTo(5));
                Assert.That(mobile.StatCap, Is.EqualTo(250));
                Assert.That(mobile.Followers, Is.EqualTo(2));
                Assert.That(mobile.FollowersMax, Is.EqualTo(5));
                Assert.That(mobile.ModifierCaps.PhysicalResist, Is.EqualTo(70));
                Assert.That(mobile.ModifierCaps.DefenseChanceIncrease, Is.EqualTo(45));
            }
        );
    }

    [Test]
    public void EquipItem_ShouldPopulatePersistedIdsAndRuntimeReference()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001000
        };
        var item = new UOItemEntity
        {
            Id = (Serial)0x40002000,
            ItemId = 0x1515,
            Hue = 0x0456
        };

        mobile.EquipItem(ItemLayerType.Shirt, item);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.EquippedItemIds[ItemLayerType.Shirt], Is.EqualTo(item.Id));
                Assert.That(mobile.TryGetEquippedReference(ItemLayerType.Shirt, out var equipped), Is.True);
                Assert.That(equipped.ItemId, Is.EqualTo(0x1515));
                Assert.That(equipped.Hue, Is.EqualTo(0x0456));
                Assert.That(item.EquippedMobileId, Is.EqualTo(mobile.Id));
                Assert.That(item.EquippedLayer, Is.EqualTo(ItemLayerType.Shirt));
            }
        );
    }

    [Test]
    public void GetEquippedItemsRuntime_ShouldReturnEquippedRuntimeItems()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001021
        };
        var shirt = new UOItemEntity
        {
            Id = (Serial)0x40002021,
            ItemId = 0x1517
        };
        var shoes = new UOItemEntity
        {
            Id = (Serial)0x40002022,
            ItemId = 0x170F
        };

        mobile.AddEquippedItem(ItemLayerType.Shirt, shirt);
        mobile.AddEquippedItem(ItemLayerType.Shoes, shoes);

        Assert.That(mobile.GetEquippedItemsRuntime(), Has.Count.EqualTo(2));
    }

    [Test]
    public void GetPacketFlags_WhenClassicClient_ShouldEncodeExpectedBits()
    {
        var mobile = new UOMobileEntity
        {
            IsParalyzed = true,
            Gender = GenderType.Female,
            IsPoisoned = true,
            IsBlessed = true,
            IgnoreMobiles = true,
            IsHidden = true
        };

        var flags = mobile.GetPacketFlags(false);

        Assert.That(flags, Is.EqualTo(0xDF));
    }

    [Test]
    public void GetPacketFlags_WhenStygianAbyssClient_ShouldUseFlyingBitInsteadOfPoisonBit()
    {
        var mobile = new UOMobileEntity
        {
            IsParalyzed = true,
            Gender = GenderType.Female,
            IsPoisoned = true,
            IsFlying = true,
            IsBlessed = true,
            IgnoreMobiles = false,
            IsHidden = false
        };

        var flags = mobile.GetPacketFlags(true);

        Assert.That(flags, Is.EqualTo(0x0F));
    }

    [Test]
    public void Gold_ShouldNotLoopOnContainerCycles()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001020
        };

        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40002010,
            ItemId = 0x0E75
        };
        var pouch = new UOItemEntity
        {
            Id = (Serial)0x40002011,
            ItemId = 0x0E79
        };
        var gold = new UOItemEntity
        {
            Id = (Serial)0x40002012,
            ItemId = 0x0EED,
            Amount = 10
        };

        backpack.AddItem(pouch, new(1, 1));
        pouch.AddItem(gold, new(2, 2));
        pouch.AddItem(backpack, new(3, 3));

        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);

        Assert.That(mobile.Gold, Is.EqualTo(10));
    }

    [Test]
    public void Gold_ShouldSumGoldInBackpackAndBankBoxIncludingNestedContainers()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001010
        };

        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40001010,
            ItemId = 0x0E75
        };
        var bankBox = new UOItemEntity
        {
            Id = (Serial)0x40001011,
            ItemId = 0x09A8
        };
        var pouch = new UOItemEntity
        {
            Id = (Serial)0x40001012,
            ItemId = 0x0E79
        };
        var goldBackpack = new UOItemEntity
        {
            Id = (Serial)0x40001013,
            ItemId = 0x0EED,
            Amount = 250
        };
        var goldPouch = new UOItemEntity
        {
            Id = (Serial)0x40001014,
            ItemId = 0x0EED,
            Amount = 100
        };
        var goldBank = new UOItemEntity
        {
            Id = (Serial)0x40001015,
            ItemId = 0x0EED,
            Amount = 700
        };

        backpack.AddItem(goldBackpack, new(1, 1));
        backpack.AddItem(pouch, new(2, 2));
        pouch.AddItem(goldPouch, new(3, 3));
        bankBox.AddItem(goldBank, new(4, 4));

        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        mobile.AddEquippedItem(ItemLayerType.Bank, bankBox);

        Assert.That(mobile.Gold, Is.EqualTo(1050));
    }

    [Test]
    public void Gold_WhenNoRuntimeBackpackOrBank_ShouldReturnZero()
    {
        var mobile = new UOMobileEntity
        {
            BackpackId = (Serial)0x40000099
        };

        Assert.That(mobile.Gold, Is.EqualTo(0));
    }

    [Test]
    public void HiddenAndBlessedAliases_ShouldMapToIsHiddenAndIsBlessed()
    {
        var mobile = new UOMobileEntity
        {
            Hidden = true,
            Blessed = true
        };

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.IsHidden, Is.True);
                Assert.That(mobile.IsBlessed, Is.True);
                mobile.IsHidden = false;
                mobile.IsBlessed = false;
                Assert.That(mobile.Hidden, Is.False);
                Assert.That(mobile.Blessed, Is.False);
            }
        );
    }

    [Test]
    public void IsMounted_ShouldReflectMountedMobileRelationship()
    {
        var mobile = new UOMobileEntity();

        Assert.That(mobile.IsMounted, Is.False);

        mobile.MountedMobileId = (Serial)0x00002000;

        Assert.That(mobile.IsMounted, Is.True);

        mobile.MountedMobileId = Serial.Zero;

        Assert.That(mobile.IsMounted, Is.False);
    }

    [Test]
    public void TryGetMountDisplayItemReference_ShouldReturnVirtualMountLayerReference_WhenConfigured()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000045u,
            MountedDisplayItemId = 0x3E9F
        };

        var found = mobile.TryGetMountDisplayItemReference(out var itemReference);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(itemReference.ItemId, Is.EqualTo(0x3E9F));
                Assert.That(itemReference.Id, Is.Not.EqualTo(Serial.Zero));
                Assert.That(itemReference.Id.IsItem, Is.True);
            }
        );
    }

    [Test]
    public void HydrateEquipmentRuntime_ShouldBuildReferencesForOwnedEquippedItems()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001002,
            EquippedItemIds = new()
            {
                [ItemLayerType.Shirt] = (Serial)0x40002010
            }
        };

        var shirt = new UOItemEntity
        {
            Id = (Serial)0x40002010,
            ItemId = 0x1517,
            Hue = 0x000A,
            EquippedMobileId = mobile.Id,
            EquippedLayer = ItemLayerType.Shirt
        };

        mobile.HydrateEquipmentRuntime([shirt]);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.TryGetEquippedReference(ItemLayerType.Shirt, out var reference), Is.True);
                Assert.That(reference.Id, Is.EqualTo(shirt.Id));
                Assert.That(reference.ItemId, Is.EqualTo(shirt.ItemId));
                Assert.That(reference.Hue, Is.EqualTo(shirt.Hue));
            }
        );
    }

    [Test]
    public void InitializeSkills_ShouldPopulateFullSkillTableWithDefaults()
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
                1,
                "Anatomy",
                100,
                0,
                0,
                "Biologist",
                0,
                0,
                0,
                1,
                "Anatomy",
                Stat.Strength,
                Stat.Intelligence
            )
        ];
        var mobile = new UOMobileEntity();

        mobile.InitializeSkills();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Skills, Has.Count.EqualTo(2));
                Assert.That(mobile.Skills[UOSkillName.Alchemy].Value, Is.EqualTo(0));
                Assert.That(mobile.Skills[UOSkillName.Alchemy].Base, Is.EqualTo(0));
                Assert.That(mobile.Skills[UOSkillName.Alchemy].Cap, Is.EqualTo(1000));
                Assert.That(mobile.Skills[UOSkillName.Alchemy].Lock, Is.EqualTo(UOSkillLock.Up));
                Assert.That(mobile.Skills[UOSkillName.Anatomy].Value, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void LegacyScalarProperties_ShouldReadAndWriteTypedBaseState()
    {
        var mobile = new UOMobileEntity();

        mobile.Strength = 60;
        mobile.Dexterity = 50;
        mobile.Intelligence = 40;
        mobile.Hits = 55;
        mobile.MaxHits = 70;
        mobile.Mana = 35;
        mobile.MaxMana = 45;
        mobile.Stamina = 25;
        mobile.MaxStamina = 65;
        mobile.FireResistance = 10;
        mobile.ColdResistance = 11;
        mobile.PoisonResistance = 12;
        mobile.EnergyResistance = 13;
        mobile.Luck = 100;

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.BaseStats.Strength, Is.EqualTo(60));
                Assert.That(mobile.BaseStats.Dexterity, Is.EqualTo(50));
                Assert.That(mobile.BaseStats.Intelligence, Is.EqualTo(40));
                Assert.That(mobile.Resources.Hits, Is.EqualTo(55));
                Assert.That(mobile.Resources.MaxHits, Is.EqualTo(70));
                Assert.That(mobile.Resources.Mana, Is.EqualTo(35));
                Assert.That(mobile.Resources.MaxMana, Is.EqualTo(45));
                Assert.That(mobile.Resources.Stamina, Is.EqualTo(25));
                Assert.That(mobile.Resources.MaxStamina, Is.EqualTo(65));
                Assert.That(mobile.BaseResistances.Fire, Is.EqualTo(10));
                Assert.That(mobile.BaseResistances.Cold, Is.EqualTo(11));
                Assert.That(mobile.BaseResistances.Poison, Is.EqualTo(12));
                Assert.That(mobile.BaseResistances.Energy, Is.EqualTo(13));
                Assert.That(mobile.BaseLuck, Is.EqualTo(100));
            }
        );
    }

    [Test]
    public void OverrideBody_ShouldReplaceCurrentBody()
    {
        var mobile = new UOMobileEntity();

        mobile.SetBody(0x0190);
        mobile.OverrideBody(0x0191);

        Assert.That((int)mobile.GetBody(), Is.EqualTo(0x0191));
    }

    [Test]
    public void RecalculateMaxStats_ShouldSetMinimumCapsAndClampCurrentValues()
    {
        var mobile = new UOMobileEntity
        {
            Strength = 0,
            Dexterity = 0,
            Intelligence = 0,
            Hits = 99,
            Stamina = 99,
            Mana = 99
        };

        mobile.RecalculateMaxStats();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.MaxHits, Is.EqualTo(1));
                Assert.That(mobile.MaxStamina, Is.EqualTo(1));
                Assert.That(mobile.MaxMana, Is.EqualTo(1));
                Assert.That(mobile.Hits, Is.EqualTo(1));
                Assert.That(mobile.Stamina, Is.EqualTo(1));
                Assert.That(mobile.Mana, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void Skills_ShouldStoreEntriesBySkillName()
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
            )
        ];
        var mobile = new UOMobileEntity();
        var entry = new SkillEntry
        {
            Skill = SkillInfo.Table[0],
            Value = 500,
            Base = 500,
            Cap = 1000,
            Lock = UOSkillLock.Locked
        };

        mobile.Skills[UOSkillName.Alchemy] = entry;

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Skills, Has.Count.EqualTo(1));
                Assert.That(mobile.Skills[UOSkillName.Alchemy].Value, Is.EqualTo(500));
                Assert.That(mobile.Skills[UOSkillName.Alchemy].Lock, Is.EqualTo(UOSkillLock.Locked));
            }
        );
    }

    [Test]
    public void TypedMobileState_ShouldBeInitializedByDefault()
    {
        var mobile = new UOMobileEntity();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.BaseStats, Is.Not.Null);
                Assert.That(mobile.BaseResistances, Is.Not.Null);
                Assert.That(mobile.Resources, Is.Not.Null);
                Assert.That(mobile.EquipmentModifiers, Is.Null);
                Assert.That(mobile.RuntimeModifiers, Is.Null);
            }
        );
    }

    [Test]
    public void UnequipItem_ShouldRemovePersistedIdsAndRuntimeReference()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001001
        };
        var item = new UOItemEntity
        {
            Id = (Serial)0x40002001,
            ItemId = 0x1516,
            Hue = 0x0234
        };

        mobile.EquipItem(ItemLayerType.Pants, item);
        var removed = mobile.UnequipItem(ItemLayerType.Pants, item);

        Assert.Multiple(
            () =>
            {
                Assert.That(removed, Is.True);
                Assert.That(mobile.HasEquippedItem(ItemLayerType.Pants), Is.False);
                Assert.That(mobile.TryGetEquippedReference(ItemLayerType.Pants, out _), Is.False);
                Assert.That(item.EquippedMobileId, Is.EqualTo(Serial.Zero));
                Assert.That(item.EquippedLayer, Is.Null);
            }
        );
    }
}
