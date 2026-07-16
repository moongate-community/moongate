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
    // Starting-stat budget of the modern client (>= 7.0.16, the 0xF8 creation packet): every stat in
    // [10, 60] and the three summing to 90. ModernUO floors a client that sends anything else.
    private const int StartingStatPoints = 90;
    private const int MinStartingStat = 10;
    private const int MaxStartingStat = 60;

    // Players carry a flat hit-point base on top of half their strength (ModernUO PlayerMobile).
    private const int PlayerBaseHits = 50;

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
        var (strength, dexterity, intelligence) = ResolveStartingStats(packet);
        var hitsMax = PlayerHitsMax(strength);

        var character = new MobileEntity
        {
            Name = packet.Name,
            Gender = packet.Gender,
            Race = packet.Race,
            Body = ResolvePlayerBody(packet.Race, packet.Gender),
            ProfessionId = packet.ProfessionId,
            Strength = strength,
            Dexterity = dexterity,
            Intelligence = intelligence,
            // Player pool ceilings, as ModernUO: hits 50 + Str/2, stamina Dex, mana Int. Pools start
            // topped up (InitStats). Regen and damage that move the current pools arrive with combat.
            Hits = hitsMax,
            HitsMax = hitsMax,
            Stamina = dexterity,
            StaminaMax = dexterity,
            Mana = intelligence,
            ManaMax = intelligence,
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

    /// <summary>
    /// Validates the client-sent starting stats the way ModernUO's CharacterCreation.SetStats does:
    /// every stat within [10, 60] and the three summing to the 90-point budget. A client sending
    /// anything else is not trusted, and every stat is floored to the minimum.
    /// </summary>
    private (int Strength, int Dexterity, int Intelligence) ResolveStartingStats(CharacterCreationPacket packet)
    {
        int strength = packet.Strength;
        int dexterity = packet.Dexterity;
        int intelligence = packet.Intelligence;

        if (strength is >= MinStartingStat and <= MaxStartingStat &&
            dexterity is >= MinStartingStat and <= MaxStartingStat &&
            intelligence is >= MinStartingStat and <= MaxStartingStat &&
            strength + dexterity + intelligence == StartingStatPoints)
        {
            return (strength, dexterity, intelligence);
        }

        _logger.Warning(
            "Character '{Name}' sent invalid starting stats {Strength}/{Dexterity}/{Intelligence}; flooring to {Floor} each",
            packet.Name,
            strength,
            dexterity,
            intelligence,
            MinStartingStat
        );

        return (MinStartingStat, MinStartingStat, MinStartingStat);
    }

    /// <summary>Player hit-point ceiling, as ModernUO's PlayerMobile: half the strength over a flat base.</summary>
    private static int PlayerHitsMax(int strength)
        => PlayerBaseHits + strength / 2;

    /// <summary>Maps a playable race and gender to the human/elf/gargoyle body graphic id.</summary>
    private static int ResolvePlayerBody(RaceType race, GenderType gender)
    {
        var female = gender == GenderType.Female;

        return race switch
        {
            RaceType.Elf      => female ? 0x25E : 0x25D,
            RaceType.Gargoyle => female ? 0x29B : 0x29A,
            _                 => female ? 0x191 : 0x190
        };
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

        var variant = PickVariant(template.Variants);

        var mobile = new MobileEntity
        {
            Name = template.Name,
            MapId = mapId,
            Position = position,
            Gender = ResolveGender(variant?.Gender ?? template.Gender),
            Strength = template.Strength,
            Dexterity = template.Dexterity,
            Intelligence = template.Intelligence,
            // Creature pool ceilings mirror the raw stats (ModernUO BaseCreature without a *MaxSeed),
            // unlike players, who get the flat hit-point base. Spawns start topped up.
            Hits = template.Strength,
            HitsMax = template.Strength,
            Stamina = template.Dexterity,
            StaminaMax = template.Dexterity,
            Mana = template.Intelligence,
            ManaMax = template.Intelligence,
            BrainScriptId = template.BrainScript ?? string.Empty,
            LootTableId = variant?.LootTableId ?? template.LootTableId ?? string.Empty
        };

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
            _                               => GenderType.Male
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
