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
            BrainId = template.Brain,
            BaseBody = (Body)template.Body,
            Location = Point3D.Zero,
            Direction = DirectionType.South,
            IsPlayer = false,
            IsAlive = true,
            RaceIndex = 0,
            SkinHue = (short)template.SkinHue.Resolve(),
            HairStyle = (short)template.HairStyle,
            HairHue = (short)template.HairHue.Resolve(),
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

        mobile.RecalculateMaxStats();
        InitializeTemplateSkills(mobile, template);
        mobile.Sounds = new(template.Sounds);
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
