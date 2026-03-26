using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Names;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Creates mobile entities from templates and character creation packets.
/// </summary>
public sealed class MobileFactoryService : IMobileFactoryService
{
    private const string SellProfileIdKey = "sell_profile_id";
    private const string VariantIndexKey = "mobile_variant_index";
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly INameService _nameService;
    private readonly IPersistenceService _persistenceService;
    private readonly ISellProfileTemplateService? _sellProfileTemplateService;

    public MobileFactoryService(
        IMobileTemplateService mobileTemplateService,
        INameService nameService,
        IPersistenceService persistenceService,
        ISellProfileTemplateService? sellProfileTemplateService = null
    )
    {
        _mobileTemplateService = mobileTemplateService;
        _nameService = nameService;
        _persistenceService = persistenceService;
        _sellProfileTemplateService = sellProfileTemplateService;
    }

    /// <inheritdoc />
    public UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileTemplateId);

        if (!_mobileTemplateService.TryGet(mobileTemplateId, out var template) || template is null)
        {
            throw new InvalidOperationException($"Mobile template '{mobileTemplateId}' not found.");
        }

        if (template.Variants.Count == 0)
        {
            throw new InvalidOperationException($"Mobile template '{mobileTemplateId}' has no variants.");
        }

        var now = DateTime.UtcNow;
        var resolvedName = template.Name;

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = _nameService.GenerateName(template);
        }

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = template.Title;
        }

        var mobile = new UOMobileEntity
        {
            Id = _persistenceService.UnitOfWork.AllocateNextMobileId(),
            AccountId = accountId ?? Serial.Zero,
            Name = resolvedName,
            Title = template.Title,
            BrainId = ResolveBrainId(template.Ai.Brain),
            Location = Point3D.Zero,
            Direction = DirectionType.South,
            IsPlayer = false,
            IsAlive = true,
            RaceIndex = 0,
            FactionId = string.IsNullOrWhiteSpace(template.DefaultFactionId) ? null : template.DefaultFactionId.Trim(),
            BaseStats = new()
            {
                Strength = template.Strength,
                Dexterity = template.Dexterity,
                Intelligence = template.Intelligence
            },
            Resources = new()
            {
                Hits = template.Hits,
                Mana = template.Mana,
                Stamina = template.Stamina
            },
            Notoriety = template.Notoriety,
            CreatedUtc = now,
            LastLoginUtc = now
        };

        ApplyVariantAppearance(mobile, template);
        mobile.RecalculateMaxStats();
        InitializeTemplateSkills(mobile, template);
        mobile.Sounds = new(template.Sounds);

        ApplyResistances(mobile, template);
        ApplyDamageTypes(mobile, template);
        ApplyAi(mobile, template);

        if (template.LootTables.Count > 0)
        {
            mobile.SetCustomString(MobileCustomParamKeys.Loot.LootTables, string.Join(',', template.LootTables));
        }

        if (template.MaxHits > 0)
        {
            mobile.MaxHits = template.MaxHits;
            mobile.Hits = Math.Min(mobile.Hits, mobile.MaxHits);
        }

        BindSellProfile(mobile, template);
        ApplyTemplateParams(mobile, template);

        return mobile;
    }

    /// <inheritdoc />
    public UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var now = DateTime.UtcNow;
        var location = packet.StartingCity?.Location ?? Point3D.Zero;
        var mapId = packet.StartingCity?.Map?.Index ?? 0;

        var mobile = new UOMobileEntity
        {
            Id = _persistenceService.UnitOfWork.AllocateNextMobileId(),
            AccountId = accountId,
            Name = packet.CharacterName,
            Location = location,
            MapId = mapId,
            Direction = DirectionType.South,
            IsPlayer = true,
            IsAlive = true,
            Gender = packet.Gender,
            RaceIndex = (byte)Math.Max(0, packet.RaceIndex),
            ProfessionId = packet.ProfessionId,
            SkinHue = packet.Skin.Hue,
            HairStyle = packet.Hair.Style,
            HairHue = packet.Hair.Hue,
            FacialHairStyle = packet.FacialHair.Style,
            FacialHairHue = packet.FacialHair.Hue,
            BaseStats = new()
            {
                Strength = packet.Strength,
                Dexterity = packet.Dexterity,
                Intelligence = packet.Intelligence
            },
            Resources = new()
            {
                Hits = packet.Strength,
                Mana = packet.Intelligence,
                Stamina = packet.Dexterity
            },
            IsWarMode = false,
            IsHidden = false,
            IsFrozen = false,
            IsPoisoned = false,
            IsBlessed = false,
            Notoriety = Notoriety.Innocent,
            CreatedUtc = now,
            LastLoginUtc = now
        };

        mobile.RecalculateMaxStats();
        mobile.InitializeSkills();

        foreach (var skill in packet.Skills)
        {
            mobile.SetSkill(skill.Skill, skill.Value * 10);
        }

        return mobile;
    }

    private static void ApplyResistances(UOMobileEntity mobile, MobileTemplateDefinition template)
    {
        if (template.Resistances.Count == 0)
        {
            return;
        }

        foreach (var (key, value) in template.Resistances)
        {
            if (string.Equals(key, "physical", StringComparison.OrdinalIgnoreCase))
            {
                mobile.BaseResistances.Physical = value;
            }
            else if (string.Equals(key, "fire", StringComparison.OrdinalIgnoreCase))
            {
                mobile.BaseResistances.Fire = value;
            }
            else if (string.Equals(key, "cold", StringComparison.OrdinalIgnoreCase))
            {
                mobile.BaseResistances.Cold = value;
            }
            else if (string.Equals(key, "poison", StringComparison.OrdinalIgnoreCase))
            {
                mobile.BaseResistances.Poison = value;
            }
            else if (string.Equals(key, "energy", StringComparison.OrdinalIgnoreCase))
            {
                mobile.BaseResistances.Energy = value;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Mobile template '{template.Id}' has unknown resistance key '{key}'."
                );
            }
        }
    }

    private static void ApplyDamageTypes(UOMobileEntity mobile, MobileTemplateDefinition template)
    {
        if (template.DamageTypes.Count == 0)
        {
            return;
        }

        var parts = template.DamageTypes.Select(kvp => $"{kvp.Key}:{kvp.Value}");
        mobile.SetCustomString(MobileCustomParamKeys.Combat.DamageTypes, string.Join(',', parts));
    }

    private static void ApplyAi(UOMobileEntity mobile, MobileTemplateDefinition template)
    {
        mobile.SetCustomString(MobileCustomParamKeys.Ai.FightMode, template.Ai.FightMode.Trim());
        mobile.SetCustomInteger(MobileCustomParamKeys.Ai.RangePerception, template.Ai.RangePerception);
        mobile.SetCustomInteger(MobileCustomParamKeys.Ai.RangeFight, template.Ai.RangeFight);
    }

    private static void ApplyVariantAppearance(UOMobileEntity mobile, MobileTemplateDefinition template)
    {
        var selectedVariantIndex = SelectVariantIndex(template);
        var variant = template.Variants[selectedVariantIndex];
        var appearance = variant.Appearance;

        if (appearance.Body > 0)
        {
            mobile.BaseBody = (Body)appearance.Body;
        }

        if (appearance.SkinHue.HasValue)
        {
            mobile.SkinHue = (short)appearance.SkinHue.Value.Resolve();
        }

        if (appearance.HairStyle > 0)
        {
            mobile.HairStyle = (short)appearance.HairStyle;
        }

        if (appearance.HairHue.HasValue)
        {
            mobile.HairHue = (short)appearance.HairHue.Value.Resolve();
        }

        if (appearance.FacialHairStyle > 0)
        {
            mobile.FacialHairStyle = (short)appearance.FacialHairStyle;
        }

        if (appearance.FacialHairHue.HasValue)
        {
            mobile.FacialHairHue = (short)appearance.FacialHairHue.Value.Resolve();
        }

        mobile.SetCustomInteger(VariantIndexKey, selectedVariantIndex);
    }

    private static void ApplyTemplateParams(UOMobileEntity mobile, MobileTemplateDefinition template)
    {
        foreach (var (key, param) in template.Params)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    $"Mobile template '{template.Id}' has an invalid params entry with an empty key."
                );
            }

            var normalizedKey = key.Trim();

            switch (param.Type)
            {
                case ItemTemplateParamType.String:
                    mobile.SetCustomString(normalizedKey, param.Value);

                    break;
                case ItemTemplateParamType.Serial:
                    if (!Serial.TryParse(param.Value, null, out var serial))
                    {
                        throw new InvalidOperationException(
                            $"Mobile template '{template.Id}' has invalid serial param '{normalizedKey}' = '{param.Value}'."
                        );
                    }

                    mobile.SetCustomInteger(normalizedKey, serial.Value);

                    break;
                case ItemTemplateParamType.Hue:
                    try
                    {
                        var resolvedHue = HueSpec.ParseFromString(param.Value).Resolve();
                        mobile.SetCustomInteger(normalizedKey, resolvedHue);
                    }
                    catch (FormatException exception)
                    {
                        throw new InvalidOperationException(
                            $"Mobile template '{template.Id}' has invalid hue param '{normalizedKey}' = '{param.Value}'.",
                            exception
                        );
                    }

                    break;
                default:
                    throw new InvalidOperationException(
                        $"Mobile template '{template.Id}' has unsupported param type '{param.Type}' for key '{normalizedKey}'."
                );
            }
        }
    }

    private static int SelectVariantIndex(MobileTemplateDefinition template)
    {
        var weightedVariants = template.Variants
                               .Select((variant, index) => (Variant: variant, Index: index))
                               .Where(static variant => variant.Variant.Weight > 0)
                               .ToArray();

        if (weightedVariants.Length == 0)
        {
            throw new InvalidOperationException($"Mobile template '{template.Id}' has no selectable variants.");
        }

        var totalWeight = weightedVariants.Sum(static variant => variant.Variant.Weight);
        var roll = Random.Shared.Next(1, totalWeight + 1);
        var runningWeight = 0;

        foreach (var variant in weightedVariants)
        {
            runningWeight += variant.Variant.Weight;

            if (roll <= runningWeight)
            {
                return variant.Index;
            }
        }

        return weightedVariants[^1].Index;
    }

    private static string? ResolveBrainId(string brainId)
    {
        if (string.IsNullOrWhiteSpace(brainId))
        {
            return null;
        }

        var normalizedBrainId = brainId.Trim();

        if (string.Equals(normalizedBrainId, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalizedBrainId;
    }

    private void BindSellProfile(UOMobileEntity mobile, MobileTemplateDefinition template)
    {
        if (string.IsNullOrWhiteSpace(template.SellProfileId))
        {
            return;
        }

        if (_sellProfileTemplateService is null ||
            !_sellProfileTemplateService.TryGet(template.SellProfileId, out _))
        {
            throw new InvalidOperationException(
                $"Mobile template '{template.Id}' references missing sell profile '{template.SellProfileId}'."
            );
        }

        mobile.SetCustomString(SellProfileIdKey, template.SellProfileId);
    }

    private static void InitializeTemplateSkills(UOMobileEntity mobile, MobileTemplateDefinition template)
    {
        mobile.InitializeSkills();

        foreach (var skill in template.Skills)
        {
            if (!TryResolveSkillName(skill.Key, out var skillName))
            {
                continue;
            }

            mobile.SetSkill(skillName, skill.Value);
        }
    }

    private static bool TryResolveSkillName(string skillName, out UOSkillName resolved)
    {
        if (ProfessionInfo.TryGetSkillName(skillName, out resolved))
        {
            return true;
        }

        foreach (var skillInfo in SkillInfo.Table)
        {
            if (string.Equals(skillInfo.Name, skillName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skillInfo.ProfessionSkillName, skillName, StringComparison.OrdinalIgnoreCase))
            {
                resolved = (UOSkillName)skillInfo.SkillID;

                return true;
            }
        }

        resolved = default;

        return false;
    }
}
