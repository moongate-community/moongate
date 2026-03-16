using Moongate.Persistence.Data.Persistence;
using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Persistence.Data.Internal;

/// <summary>
/// Converts between runtime entities and persistence snapshots.
/// </summary>
internal static class SnapshotMapper
{
    public static UOAccountEntity ToAccountEntity(AccountSnapshot snapshot)
        => new()
        {
            Id = (Serial)snapshot.Id,
            Username = snapshot.Username,
            PasswordHash = snapshot.PasswordHash,
            Email = snapshot.Email,
            AccountType = (AccountType)snapshot.AccountType,
            IsLocked = snapshot.IsLocked,
            ActivationId = snapshot.ActivationId,
            RecoveryCode = snapshot.RecoveryCode,
            CreatedUtc = new(snapshot.CreatedUtcTicks, DateTimeKind.Utc),
            LastLoginUtc = new(snapshot.LastLoginUtcTicks, DateTimeKind.Utc),
            CharacterIds = [.. snapshot.CharacterIds.Select(id => (Serial)id)]
        };

    public static AccountSnapshot ToAccountSnapshot(UOAccountEntity entity)
        => new()
        {
            Id = (uint)entity.Id,
            Username = entity.Username,
            PasswordHash = entity.PasswordHash,
            Email = entity.Email,
            AccountType = (byte)entity.AccountType,
            IsLocked = entity.IsLocked,
            ActivationId = entity.ActivationId,
            RecoveryCode = entity.RecoveryCode,
            CreatedUtcTicks = entity.CreatedUtc.Ticks,
            LastLoginUtcTicks = entity.LastLoginUtc.Ticks,
            CharacterIds = [.. entity.CharacterIds.Select(serial => (uint)serial)]
        };

    public static BulletinBoardMessageEntity ToBulletinBoardMessageEntity(BulletinBoardMessageSnapshot snapshot)
    {
        var entity = new BulletinBoardMessageEntity
        {
            MessageId = (Serial)snapshot.MessageId,
            BoardId = (Serial)snapshot.BoardId,
            ParentId = (Serial)snapshot.ParentId,
            OwnerCharacterId = (Serial)snapshot.OwnerCharacterId,
            Author = snapshot.Author,
            Subject = snapshot.Subject,
            PostedAtUtc = new(snapshot.PostedAtUtcTicks, DateTimeKind.Utc)
        };

        if (snapshot.BodyLines.Length > 0)
        {
            entity.BodyLines.AddRange(snapshot.BodyLines);
        }

        return entity;
    }

    public static BulletinBoardMessageSnapshot ToBulletinBoardMessageSnapshot(BulletinBoardMessageEntity entity)
        => new()
        {
            MessageId = (uint)entity.MessageId,
            BoardId = (uint)entity.BoardId,
            ParentId = (uint)entity.ParentId,
            OwnerCharacterId = (uint)entity.OwnerCharacterId,
            Author = entity.Author,
            Subject = entity.Subject,
            PostedAtUtcTicks = entity.PostedAtUtc.Ticks,
            BodyLines = [.. entity.BodyLines]
        };

    public static UOItemEntity ToItemEntity(ItemSnapshot snapshot)
    {
        var entity = new UOItemEntity
        {
            Id = (Serial)snapshot.Id,
            Location = new(snapshot.X, snapshot.Y, snapshot.Z),
            MapId = snapshot.MapId,
            Name = snapshot.Name,
            Weight = snapshot.Weight,
            Amount = snapshot.Amount <= 0 ? 1 : snapshot.Amount,
            IsStackable = snapshot.IsStackable,
            Rarity = (ItemRarity)snapshot.Rarity,
            Visibility = (AccountType)snapshot.Visibility,
            ItemId = snapshot.ItemId,
            Hue = snapshot.Hue,
            GumpId = snapshot.GumpId,
            Direction = (DirectionType)snapshot.Direction,
            ScriptId = snapshot.ScriptId,
            ParentContainerId = (Serial)snapshot.ParentContainerId,
            ContainerPosition = new(snapshot.ContainerX, snapshot.ContainerY),
            EquippedMobileId = (Serial)snapshot.EquippedMobileId,
            EquippedLayer = snapshot.EquippedLayer is null ? null : (ItemLayerType)snapshot.EquippedLayer.Value,
            CombatStats = snapshot.CombatStats is null
                              ? null
                              : new()
                              {
                                  MinStrength = snapshot.CombatStats.MinStrength,
                                  MinDexterity = snapshot.CombatStats.MinDexterity,
                                  MinIntelligence = snapshot.CombatStats.MinIntelligence,
                                  DamageMin = snapshot.CombatStats.DamageMin,
                                  DamageMax = snapshot.CombatStats.DamageMax,
                                  Defense = snapshot.CombatStats.Defense,
                                  AttackSpeed = snapshot.CombatStats.AttackSpeed,
                                  RangeMin = snapshot.CombatStats.RangeMin,
                                  RangeMax = snapshot.CombatStats.RangeMax,
                                  MaxDurability = snapshot.CombatStats.MaxDurability,
                                  CurrentDurability = snapshot.CombatStats.CurrentDurability
                              },
            Modifiers = snapshot.Modifiers is null
                            ? null
                            : new()
                            {
                                StrengthBonus = snapshot.Modifiers.StrengthBonus,
                                DexterityBonus = snapshot.Modifiers.DexterityBonus,
                                IntelligenceBonus = snapshot.Modifiers.IntelligenceBonus,
                                PhysicalResist = snapshot.Modifiers.PhysicalResist,
                                FireResist = snapshot.Modifiers.FireResist,
                                ColdResist = snapshot.Modifiers.ColdResist,
                                PoisonResist = snapshot.Modifiers.PoisonResist,
                                EnergyResist = snapshot.Modifiers.EnergyResist,
                                HitChanceIncrease = snapshot.Modifiers.HitChanceIncrease,
                                DefenseChanceIncrease = snapshot.Modifiers.DefenseChanceIncrease,
                                DamageIncrease = snapshot.Modifiers.DamageIncrease,
                                SwingSpeedIncrease = snapshot.Modifiers.SwingSpeedIncrease,
                                SpellDamageIncrease = snapshot.Modifiers.SpellDamageIncrease,
                                FasterCasting = snapshot.Modifiers.FasterCasting,
                                FasterCastRecovery = snapshot.Modifiers.FasterCastRecovery,
                                LowerManaCost = snapshot.Modifiers.LowerManaCost,
                                LowerReagentCost = snapshot.Modifiers.LowerReagentCost,
                                Luck = snapshot.Modifiers.Luck,
                                SpellChanneling = snapshot.Modifiers.SpellChanneling,
                                UsesRemaining = snapshot.Modifiers.UsesRemaining
                            }
        };

        if (snapshot.ContainedItemIds is { Length: > 0 })
        {
            entity.ContainedItemIds = [.. snapshot.ContainedItemIds.Select(id => (Serial)id)];
        }

        if (snapshot.CustomProperties is { Length: > 0 })
        {
            foreach (var customProperty in snapshot.CustomProperties)
            {
                entity.SetCustomProperty(
                    customProperty.Key,
                    new()
                    {
                        Type = (ItemCustomPropertyType)customProperty.Type,
                        IntegerValue = customProperty.IntegerValue,
                        BooleanValue = customProperty.BooleanValue,
                        DoubleValue = customProperty.DoubleValue,
                        StringValue = customProperty.StringValue
                    }
                );
            }
        }

        return entity;
    }

    public static ItemSnapshot ToItemSnapshot(UOItemEntity entity)
        => new()
        {
            Id = (uint)entity.Id,
            X = entity.Location.X,
            Y = entity.Location.Y,
            Z = entity.Location.Z,
            MapId = entity.MapId,
            Name = entity.Name,
            Weight = entity.Weight,
            Amount = entity.Amount <= 0 ? 1 : entity.Amount,
            IsStackable = entity.IsStackable,
            Rarity = (byte)entity.Rarity,
            Visibility = (byte)entity.Visibility,
            ItemId = entity.ItemId,
            Hue = entity.Hue,
            GumpId = entity.GumpId,
            Direction = (byte)entity.Direction,
            ScriptId = entity.ScriptId,
            ParentContainerId = (uint)entity.ParentContainerId,
            ContainerX = entity.ContainerPosition.X,
            ContainerY = entity.ContainerPosition.Y,
            EquippedMobileId = (uint)entity.EquippedMobileId,
            EquippedLayer = entity.EquippedLayer is null ? null : (byte)entity.EquippedLayer.Value,
            ContainedItemIds = [.. entity.ContainedItemIds.Select(id => (uint)id)],
            CombatStats = entity.CombatStats is null
                              ? null
                              : new()
                              {
                                  MinStrength = entity.CombatStats.MinStrength,
                                  MinDexterity = entity.CombatStats.MinDexterity,
                                  MinIntelligence = entity.CombatStats.MinIntelligence,
                                  DamageMin = entity.CombatStats.DamageMin,
                                  DamageMax = entity.CombatStats.DamageMax,
                                  Defense = entity.CombatStats.Defense,
                                  AttackSpeed = entity.CombatStats.AttackSpeed,
                                  RangeMin = entity.CombatStats.RangeMin,
                                  RangeMax = entity.CombatStats.RangeMax,
                                  MaxDurability = entity.CombatStats.MaxDurability,
                                  CurrentDurability = entity.CombatStats.CurrentDurability
                              },
            Modifiers = entity.Modifiers is null
                            ? null
                            : new()
                            {
                                StrengthBonus = entity.Modifiers.StrengthBonus,
                                DexterityBonus = entity.Modifiers.DexterityBonus,
                                IntelligenceBonus = entity.Modifiers.IntelligenceBonus,
                                PhysicalResist = entity.Modifiers.PhysicalResist,
                                FireResist = entity.Modifiers.FireResist,
                                ColdResist = entity.Modifiers.ColdResist,
                                PoisonResist = entity.Modifiers.PoisonResist,
                                EnergyResist = entity.Modifiers.EnergyResist,
                                HitChanceIncrease = entity.Modifiers.HitChanceIncrease,
                                DefenseChanceIncrease = entity.Modifiers.DefenseChanceIncrease,
                                DamageIncrease = entity.Modifiers.DamageIncrease,
                                SwingSpeedIncrease = entity.Modifiers.SwingSpeedIncrease,
                                SpellDamageIncrease = entity.Modifiers.SpellDamageIncrease,
                                FasterCasting = entity.Modifiers.FasterCasting,
                                FasterCastRecovery = entity.Modifiers.FasterCastRecovery,
                                LowerManaCost = entity.Modifiers.LowerManaCost,
                                LowerReagentCost = entity.Modifiers.LowerReagentCost,
                                Luck = entity.Modifiers.Luck,
                                SpellChanneling = entity.Modifiers.SpellChanneling,
                                UsesRemaining = entity.Modifiers.UsesRemaining
                            },
            CustomProperties =
            [
                .. entity.CustomProperties.Select(
                    static pair => new ItemCustomPropertySnapshot
                    {
                        Key = pair.Key,
                        Type = (byte)pair.Value.Type,
                        IntegerValue = pair.Value.IntegerValue,
                        BooleanValue = pair.Value.BooleanValue,
                        DoubleValue = pair.Value.DoubleValue,
                        StringValue = pair.Value.StringValue
                    }
                )
            ]
        };

    public static UOMobileEntity ToMobileEntity(MobileSnapshot snapshot)
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)snapshot.Id,
            AccountId = (Serial)snapshot.AccountId,
            Name = snapshot.Name,
            Title = snapshot.Title,
            BrainId = snapshot.BrainId,
            Location = new(snapshot.X, snapshot.Y, snapshot.Z),
            MapId = snapshot.MapId,
            Direction = (DirectionType)snapshot.Direction,
            IsPlayer = snapshot.IsPlayer,
            IsAlive = snapshot.IsAlive,
            Gender = (GenderType)snapshot.Gender,
            RaceIndex = snapshot.RaceIndex,
            ProfessionId = snapshot.ProfessionId,
            SkinHue = snapshot.SkinHue,
            HairStyle = snapshot.HairStyle,
            HairHue = snapshot.HairHue,
            FacialHairStyle = snapshot.FacialHairStyle,
            FacialHairHue = snapshot.FacialHairHue,
            BaseStats = snapshot.BaseStats is null
                            ? new()
                            {
                                Strength = snapshot.Strength,
                                Dexterity = snapshot.Dexterity,
                                Intelligence = snapshot.Intelligence
                            }
                            : new()
                            {
                                Strength = snapshot.BaseStats.Strength,
                                Dexterity = snapshot.BaseStats.Dexterity,
                                Intelligence = snapshot.BaseStats.Intelligence
                            },
            Resources = snapshot.Resources is null
                            ? new()
                            {
                                Hits = snapshot.Hits,
                                Mana = snapshot.Mana,
                                Stamina = snapshot.Stamina,
                                MaxHits = snapshot.MaxHits,
                                MaxMana = snapshot.MaxMana,
                                MaxStamina = snapshot.MaxStamina
                            }
                            : new()
                            {
                                Hits = snapshot.Resources.Hits,
                                Mana = snapshot.Resources.Mana,
                                Stamina = snapshot.Resources.Stamina,
                                MaxHits = snapshot.Resources.MaxHits,
                                MaxMana = snapshot.Resources.MaxMana,
                                MaxStamina = snapshot.Resources.MaxStamina
                            },
            SkillPoints = snapshot.SkillPoints,
            StatPoints = snapshot.StatPoints,
            StatCap = snapshot.StatCap,
            Followers = snapshot.Followers,
            FollowersMax = snapshot.FollowersMax,
            Weight = snapshot.Weight,
            MaxWeight = snapshot.MaxWeight,
            MinWeaponDamage = snapshot.MinWeaponDamage,
            MaxWeaponDamage = snapshot.MaxWeaponDamage,
            Tithing = snapshot.Tithing,
            BaseResistances = snapshot.BaseResistances is null
                                  ? new()
                                  {
                                      Fire = snapshot.FireResistance,
                                      Cold = snapshot.ColdResistance,
                                      Poison = snapshot.PoisonResistance,
                                      Energy = snapshot.EnergyResistance
                                  }
                                  : new()
                                  {
                                      Physical = snapshot.BaseResistances.Physical,
                                      Fire = snapshot.BaseResistances.Fire,
                                      Cold = snapshot.BaseResistances.Cold,
                                      Poison = snapshot.BaseResistances.Poison,
                                      Energy = snapshot.BaseResistances.Energy
                                  },
            BaseLuck = snapshot.BaseLuck != 0 ? snapshot.BaseLuck : snapshot.Luck,
            EquipmentModifiers = snapshot.EquipmentModifiers is null
                                     ? null
                                     : new()
                                     {
                                         StrengthBonus = snapshot.EquipmentModifiers.StrengthBonus,
                                         DexterityBonus = snapshot.EquipmentModifiers.DexterityBonus,
                                         IntelligenceBonus = snapshot.EquipmentModifiers.IntelligenceBonus,
                                         PhysicalResist = snapshot.EquipmentModifiers.PhysicalResist,
                                         FireResist = snapshot.EquipmentModifiers.FireResist,
                                         ColdResist = snapshot.EquipmentModifiers.ColdResist,
                                         PoisonResist = snapshot.EquipmentModifiers.PoisonResist,
                                         EnergyResist = snapshot.EquipmentModifiers.EnergyResist,
                                         HitChanceIncrease = snapshot.EquipmentModifiers.HitChanceIncrease,
                                         DefenseChanceIncrease = snapshot.EquipmentModifiers.DefenseChanceIncrease,
                                         DamageIncrease = snapshot.EquipmentModifiers.DamageIncrease,
                                         SwingSpeedIncrease = snapshot.EquipmentModifiers.SwingSpeedIncrease,
                                         SpellDamageIncrease = snapshot.EquipmentModifiers.SpellDamageIncrease,
                                         FasterCasting = snapshot.EquipmentModifiers.FasterCasting,
                                         FasterCastRecovery = snapshot.EquipmentModifiers.FasterCastRecovery,
                                         LowerManaCost = snapshot.EquipmentModifiers.LowerManaCost,
                                         LowerReagentCost = snapshot.EquipmentModifiers.LowerReagentCost,
                                         Luck = snapshot.EquipmentModifiers.Luck,
                                         SpellChanneling = snapshot.EquipmentModifiers.SpellChanneling
                                     },
            RuntimeModifiers = snapshot.RuntimeModifiers is null
                                   ? null
                                   : new()
                                   {
                                       StrengthBonus = snapshot.RuntimeModifiers.StrengthBonus,
                                       DexterityBonus = snapshot.RuntimeModifiers.DexterityBonus,
                                       IntelligenceBonus = snapshot.RuntimeModifiers.IntelligenceBonus,
                                       PhysicalResist = snapshot.RuntimeModifiers.PhysicalResist,
                                       FireResist = snapshot.RuntimeModifiers.FireResist,
                                       ColdResist = snapshot.RuntimeModifiers.ColdResist,
                                       PoisonResist = snapshot.RuntimeModifiers.PoisonResist,
                                       EnergyResist = snapshot.RuntimeModifiers.EnergyResist,
                                       HitChanceIncrease = snapshot.RuntimeModifiers.HitChanceIncrease,
                                       DefenseChanceIncrease = snapshot.RuntimeModifiers.DefenseChanceIncrease,
                                       DamageIncrease = snapshot.RuntimeModifiers.DamageIncrease,
                                       SwingSpeedIncrease = snapshot.RuntimeModifiers.SwingSpeedIncrease,
                                       SpellDamageIncrease = snapshot.RuntimeModifiers.SpellDamageIncrease,
                                       FasterCasting = snapshot.RuntimeModifiers.FasterCasting,
                                       FasterCastRecovery = snapshot.RuntimeModifiers.FasterCastRecovery,
                                       LowerManaCost = snapshot.RuntimeModifiers.LowerManaCost,
                                       LowerReagentCost = snapshot.RuntimeModifiers.LowerReagentCost,
                                       Luck = snapshot.RuntimeModifiers.Luck,
                                       SpellChanneling = snapshot.RuntimeModifiers.SpellChanneling
                                   },
            ModifierCaps = snapshot.ModifierCaps is null
                               ? new()
                               : new()
                               {
                                   PhysicalResist = snapshot.ModifierCaps.PhysicalResist,
                                   FireResist = snapshot.ModifierCaps.FireResist,
                                   ColdResist = snapshot.ModifierCaps.ColdResist,
                                   PoisonResist = snapshot.ModifierCaps.PoisonResist,
                                   EnergyResist = snapshot.ModifierCaps.EnergyResist,
                                   DefenseChanceIncrease = snapshot.ModifierCaps.DefenseChanceIncrease
                               },
            BaseBody = snapshot.BaseBodyId is null ? null : (Body)snapshot.BaseBodyId.Value,
            BackpackId = (Serial)snapshot.BackpackId,
            IsWarMode = snapshot.IsWarMode,
            Hunger = snapshot.Hunger,
            Thirst = snapshot.Thirst,
            Fame = snapshot.Fame,
            Karma = snapshot.Karma,
            Kills = snapshot.Kills,
            IsHidden = snapshot.IsHidden,
            IsFrozen = snapshot.IsFrozen,
            IsParalyzed = snapshot.IsParalyzed,
            IsFlying = snapshot.IsFlying,
            IgnoreMobiles = snapshot.IgnoreMobiles,
            IsPoisoned = snapshot.IsPoisoned,
            IsBlessed = snapshot.IsBlessed,
            IsInvulnerable = snapshot.IsInvulnerable,
            IsMounted = snapshot.IsMounted,
            Notoriety = (Notoriety)snapshot.Notoriety,
            CreatedUtc = new(snapshot.CreatedUtcTicks, DateTimeKind.Utc),
            LastLoginUtc = new(snapshot.LastLoginUtcTicks, DateTimeKind.Utc)
        };

        var length = Math.Min(snapshot.EquippedLayers.Length, snapshot.EquippedItemIds.Length);

        for (var i = 0; i < length; i++)
        {
            entity.EquippedItemIds[(ItemLayerType)snapshot.EquippedLayers[i]] = (Serial)snapshot.EquippedItemIds[i];
        }

        if (snapshot.CustomProperties is { Length: > 0 })
        {
            foreach (var customProperty in snapshot.CustomProperties)
            {
                entity.SetCustomProperty(
                    customProperty.Key,
                    new()
                    {
                        Type = (ItemCustomPropertyType)customProperty.Type,
                        IntegerValue = customProperty.IntegerValue,
                        BooleanValue = customProperty.BooleanValue,
                        DoubleValue = customProperty.DoubleValue,
                        StringValue = customProperty.StringValue
                    }
                );
            }
        }

        if (snapshot.Skills is { Length: > 0 })
        {
            foreach (var skill in snapshot.Skills)
            {
                if (!Enum.IsDefined(typeof(UOSkillName), skill.SkillId))
                {
                    continue;
                }

                entity.SetSkill(
                    (UOSkillName)skill.SkillId,
                    (int)skill.Value,
                    (int)skill.Base,
                    skill.Cap,
                    (UOSkillLock)skill.Lock
                );
            }
        }

        return entity;
    }

    public static MobileSnapshot ToMobileSnapshot(UOMobileEntity entity)
    {
        var equippedCount = entity.EquippedItemIds.Count;
        var layers = new byte[equippedCount];
        var itemIds = new uint[equippedCount];
        var index = 0;

        foreach (var pair in entity.EquippedItemIds.OrderBy(static pair => (int)pair.Key))
        {
            layers[index] = (byte)pair.Key;
            itemIds[index] = (uint)pair.Value;
            index++;
        }

        var customProps = new ItemCustomPropertySnapshot[entity.CustomProperties.Count];
        var propIndex = 0;

        foreach (var pair in entity.CustomProperties)
        {
            customProps[propIndex++] = new()
            {
                Key = pair.Key,
                Type = (byte)pair.Value.Type,
                IntegerValue = pair.Value.IntegerValue,
                BooleanValue = pair.Value.BooleanValue,
                DoubleValue = pair.Value.DoubleValue,
                StringValue = pair.Value.StringValue
            };
        }

        var skills = entity.Skills
                           .OrderBy(static pair => (int)pair.Key)
                           .Select(
                               static pair => new MobileSkillEntrySnapshot
                               {
                                   SkillId = (int)pair.Key,
                                   Value = pair.Value.Value,
                                   Base = pair.Value.Base,
                                   Cap = pair.Value.Cap,
                                   Lock = (byte)pair.Value.Lock
                               }
                           )
                           .ToArray();

        return new()
        {
            Id = (uint)entity.Id,
            AccountId = (uint)entity.AccountId,
            Name = entity.Name,
            Title = entity.Title,
            BrainId = entity.BrainId,
            X = entity.Location.X,
            Y = entity.Location.Y,
            Z = entity.Location.Z,
            MapId = entity.MapId,
            Direction = (byte)entity.Direction,
            IsPlayer = entity.IsPlayer,
            IsAlive = entity.IsAlive,
            Gender = (byte)entity.Gender,
            RaceIndex = entity.RaceIndex,
            ProfessionId = entity.ProfessionId,
            SkinHue = entity.SkinHue,
            HairStyle = entity.HairStyle,
            HairHue = entity.HairHue,
            FacialHairStyle = entity.FacialHairStyle,
            FacialHairHue = entity.FacialHairHue,
            Strength = entity.Strength,
            Dexterity = entity.Dexterity,
            Intelligence = entity.Intelligence,
            Hits = entity.Hits,
            Mana = entity.Mana,
            Stamina = entity.Stamina,
            MaxHits = entity.MaxHits,
            MaxMana = entity.MaxMana,
            MaxStamina = entity.MaxStamina,
            SkillPoints = entity.SkillPoints,
            StatPoints = entity.StatPoints,
            StatCap = entity.StatCap,
            Followers = entity.Followers,
            FollowersMax = entity.FollowersMax,
            Weight = entity.Weight,
            MaxWeight = entity.MaxWeight,
            MinWeaponDamage = entity.MinWeaponDamage,
            MaxWeaponDamage = entity.MaxWeaponDamage,
            Tithing = entity.Tithing,
            FireResistance = entity.FireResistance,
            ColdResistance = entity.ColdResistance,
            PoisonResistance = entity.PoisonResistance,
            EnergyResistance = entity.EnergyResistance,
            Luck = entity.Luck,
            BaseStats = new()
            {
                Strength = entity.BaseStats.Strength,
                Dexterity = entity.BaseStats.Dexterity,
                Intelligence = entity.BaseStats.Intelligence
            },
            BaseResistances = new()
            {
                Physical = entity.BaseResistances.Physical,
                Fire = entity.BaseResistances.Fire,
                Cold = entity.BaseResistances.Cold,
                Poison = entity.BaseResistances.Poison,
                Energy = entity.BaseResistances.Energy
            },
            Resources = new()
            {
                Hits = entity.Resources.Hits,
                Mana = entity.Resources.Mana,
                Stamina = entity.Resources.Stamina,
                MaxHits = entity.Resources.MaxHits,
                MaxMana = entity.Resources.MaxMana,
                MaxStamina = entity.Resources.MaxStamina
            },
            EquipmentModifiers = entity.EquipmentModifiers is null
                                     ? null
                                     : new()
                                     {
                                         StrengthBonus = entity.EquipmentModifiers.StrengthBonus,
                                         DexterityBonus = entity.EquipmentModifiers.DexterityBonus,
                                         IntelligenceBonus = entity.EquipmentModifiers.IntelligenceBonus,
                                         PhysicalResist = entity.EquipmentModifiers.PhysicalResist,
                                         FireResist = entity.EquipmentModifiers.FireResist,
                                         ColdResist = entity.EquipmentModifiers.ColdResist,
                                         PoisonResist = entity.EquipmentModifiers.PoisonResist,
                                         EnergyResist = entity.EquipmentModifiers.EnergyResist,
                                         HitChanceIncrease = entity.EquipmentModifiers.HitChanceIncrease,
                                         DefenseChanceIncrease = entity.EquipmentModifiers.DefenseChanceIncrease,
                                         DamageIncrease = entity.EquipmentModifiers.DamageIncrease,
                                         SwingSpeedIncrease = entity.EquipmentModifiers.SwingSpeedIncrease,
                                         SpellDamageIncrease = entity.EquipmentModifiers.SpellDamageIncrease,
                                         FasterCasting = entity.EquipmentModifiers.FasterCasting,
                                         FasterCastRecovery = entity.EquipmentModifiers.FasterCastRecovery,
                                         LowerManaCost = entity.EquipmentModifiers.LowerManaCost,
                                         LowerReagentCost = entity.EquipmentModifiers.LowerReagentCost,
                                         Luck = entity.EquipmentModifiers.Luck,
                                         SpellChanneling = entity.EquipmentModifiers.SpellChanneling
                                     },
            RuntimeModifiers = entity.RuntimeModifiers is null
                                   ? null
                                   : new()
                                   {
                                       StrengthBonus = entity.RuntimeModifiers.StrengthBonus,
                                       DexterityBonus = entity.RuntimeModifiers.DexterityBonus,
                                       IntelligenceBonus = entity.RuntimeModifiers.IntelligenceBonus,
                                       PhysicalResist = entity.RuntimeModifiers.PhysicalResist,
                                       FireResist = entity.RuntimeModifiers.FireResist,
                                       ColdResist = entity.RuntimeModifiers.ColdResist,
                                       PoisonResist = entity.RuntimeModifiers.PoisonResist,
                                       EnergyResist = entity.RuntimeModifiers.EnergyResist,
                                       HitChanceIncrease = entity.RuntimeModifiers.HitChanceIncrease,
                                       DefenseChanceIncrease = entity.RuntimeModifiers.DefenseChanceIncrease,
                                       DamageIncrease = entity.RuntimeModifiers.DamageIncrease,
                                       SwingSpeedIncrease = entity.RuntimeModifiers.SwingSpeedIncrease,
                                       SpellDamageIncrease = entity.RuntimeModifiers.SpellDamageIncrease,
                                       FasterCasting = entity.RuntimeModifiers.FasterCasting,
                                       FasterCastRecovery = entity.RuntimeModifiers.FasterCastRecovery,
                                       LowerManaCost = entity.RuntimeModifiers.LowerManaCost,
                                       LowerReagentCost = entity.RuntimeModifiers.LowerReagentCost,
                                       Luck = entity.RuntimeModifiers.Luck,
                                       SpellChanneling = entity.RuntimeModifiers.SpellChanneling
                                   },
            ModifierCaps = new()
            {
                PhysicalResist = entity.ModifierCaps.PhysicalResist,
                FireResist = entity.ModifierCaps.FireResist,
                ColdResist = entity.ModifierCaps.ColdResist,
                PoisonResist = entity.ModifierCaps.PoisonResist,
                EnergyResist = entity.ModifierCaps.EnergyResist,
                DefenseChanceIncrease = entity.ModifierCaps.DefenseChanceIncrease
            },
            Skills = skills,
            BaseLuck = entity.BaseLuck,
            BaseBodyId = entity.BaseBody is null ? null : (int)entity.BaseBody.Value,
            BackpackId = (uint)entity.BackpackId,
            EquippedLayers = layers,
            EquippedItemIds = itemIds,
            CustomProperties = customProps,
            IsWarMode = entity.IsWarMode,
            Hunger = entity.Hunger,
            Thirst = entity.Thirst,
            Fame = entity.Fame,
            Karma = entity.Karma,
            Kills = entity.Kills,
            IsHidden = entity.IsHidden,
            IsFrozen = entity.IsFrozen,
            IsParalyzed = entity.IsParalyzed,
            IsFlying = entity.IsFlying,
            IgnoreMobiles = entity.IgnoreMobiles,
            IsPoisoned = entity.IsPoisoned,
            IsBlessed = entity.IsBlessed,
            IsInvulnerable = entity.IsInvulnerable,
            IsMounted = entity.IsMounted,
            Notoriety = (byte)entity.Notoriety,
            CreatedUtcTicks = entity.CreatedUtc.Ticks,
            LastLoginUtcTicks = entity.LastLoginUtc.Ticks
        };
    }
}
