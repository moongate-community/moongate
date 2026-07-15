using Moongate.Core.Geometry;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Internal.Mobiles;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.Server.Interfaces.World;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Mobiles.Templates;
using Moongate.UO.Data.Types;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Services.Mobiles;

/// <summary>Default <see cref="IMobileFactoryService" />: maps protocol input and templates into mobile entities.</summary>
public sealed class MobileFactoryService : IMobileFactoryService
{
    private readonly ILogger _logger = Log.ForContext<MobileFactoryService>();
    private readonly IStartingCityService _startingCityService;
    private readonly IMobileTemplateService _templates;
    private readonly Random _random;

    public MobileFactoryService(IStartingCityService startingCityService, IMobileTemplateService templates, Random random)
    {
        _startingCityService = startingCityService;
        _templates = templates;
        _random = random;
    }

    public MobileEntity CreatePlayerMobile(CharacterCreationPacket packet)
    {
        var character = new MobileEntity
        {
            Name = packet.Name,
            Gender = packet.Gender,
            Race = packet.Race,
            ProfessionId = packet.ProfessionId,
            Strength = packet.Strength,
            Dexterity = packet.Dexterity,
            Intelligence = packet.Intelligence,
            SkinHue = new((ushort)packet.SkinHue),
            HairStyle = (ushort)packet.HairStyle,
            HairHue = new((ushort)packet.HairHue),
            FacialHairStyle = (ushort)packet.FacialHairStyle,
            FacialHairHue = new((ushort)packet.FacialHairHue)
        };

        foreach (var skill in packet.Skills)
        {
            if (skill.Value == 0)
            {
                continue; // unused starting-skill slot
            }

            character.Skills[skill.SkillId] = skill.Value * 10; // stored in tenths (50 -> 500)
        }

        // Fall back to the first city when the client sends an out-of-range index.
        var startingCity = _startingCityService.GetByIndex(packet.StartingCityIndex) ?? _startingCityService.GetByIndex(0);

        if (startingCity is not null)
        {
            character.MapId = (int)startingCity.Map;
            character.Position = new(startingCity.X, startingCity.Y, startingCity.Z);
        }

        return character;
    }

    public MobileEntity Create(string name, int mapId, Point3D position)
    {
        return new MobileEntity
        {
            Name = name,
            MapId = mapId,
            Position = position
        };
    }

    public MobileSpawn? CreateFromTemplate(string templateId, int mapId, Point3D position)
    {
        var template = _templates.GetById(templateId);

        if (template is null)
        {
            return null;
        }

        var mobile = new MobileEntity
        {
            Name = template.Name,
            MapId = mapId,
            Position = position,
            Gender = ResolveGender(template.Gender),
            Strength = template.Strength,
            Dexterity = template.Dexterity,
            Intelligence = template.Intelligence,
            BrainScriptId = template.BrainScript ?? string.Empty,
            LootTableId = template.LootTableId ?? string.Empty
        };

        var variant = PickVariant(template.Variants);
        ApplyAppearance(mobile, template.Appearance, variant?.Appearance);
        ApplySkills(mobile, template.Skills);

        var equipmentSource = variant is { Equipment.Count: > 0 } ? variant.Equipment : template.Equipment;

        return new MobileSpawn(mobile, ResolveEquipment(equipmentSource));
    }

    private GenderType ResolveGender(MobileTemplateGenderType gender)
        => gender switch
        {
            MobileTemplateGenderType.Female => GenderType.Female,
            MobileTemplateGenderType.Random => _random.Next(2) == 0 ? GenderType.Male : GenderType.Female,
            _ => GenderType.Male
        };

    private MobileVariant? PickVariant(List<MobileVariant> variants)
    {
        if (variants.Count == 0)
        {
            return null;
        }

        var total = variants.Sum(variant => Math.Max(1, variant.Weight));
        var roll = _random.Next(total);
        var cumulative = 0;

        foreach (var variant in variants)
        {
            cumulative += Math.Max(1, variant.Weight);

            if (roll < cumulative)
            {
                return variant;
            }
        }

        return variants[^1];
    }

    private void ApplyAppearance(MobileEntity mobile, MobileAppearance baseAppearance, MobileAppearance? variant)
    {
        mobile.Body = variant is { Body: not 0 } ? variant.Body : baseAppearance.Body;
        mobile.HairStyle = (ushort)(variant is { HairStyle: not 0 } ? variant.HairStyle : baseAppearance.HairStyle);
        mobile.FacialHairStyle =
            (ushort)(variant is { FacialHairStyle: not 0 } ? variant.FacialHairStyle : baseAppearance.FacialHairStyle);
        mobile.SkinHue = new Hue(HueSpec.Resolve(variant?.SkinHue ?? baseAppearance.SkinHue, _random));
        mobile.HairHue = new Hue(HueSpec.Resolve(variant?.HairHue ?? baseAppearance.HairHue, _random));
        mobile.FacialHairHue = new Hue(HueSpec.Resolve(variant?.FacialHairHue ?? baseAppearance.FacialHairHue, _random));
    }

    private void ApplySkills(MobileEntity mobile, Dictionary<string, int> skills)
    {
        foreach (var (name, value) in skills)
        {
            if (TryResolveSkill(name, out var skillId))
            {
                mobile.Skills[skillId] = value;
            }
            else
            {
                _logger.Warning("Unknown skill '{Skill}' on mobile template; skipping", name);
            }
        }
    }

    private List<ResolvedEquipment> ResolveEquipment(List<MobileEquipmentEntry> entries)
    {
        var resolved = new List<ResolvedEquipment>();

        foreach (var entry in entries)
        {
            if (!Enum.TryParse<LayerType>(entry.Layer, ignoreCase: true, out var layer))
            {
                _logger.Warning("Unknown layer '{Layer}' on mobile template equipment; skipping", entry.Layer);

                continue;
            }

            resolved.Add(new(entry.Item, layer, HueSpec.Resolve(entry.Hue, _random)));
        }

        return resolved;
    }

    private static bool TryResolveSkill(string name, out int skillId)
    {
        skillId = 0;
        var token = new string(name.Where(char.IsLetter).ToArray());

        if (!Enum.TryParse<SkillName>(token, ignoreCase: true, out var parsed))
        {
            return false;
        }

        skillId = (int)parsed;

        return true;
    }
}
