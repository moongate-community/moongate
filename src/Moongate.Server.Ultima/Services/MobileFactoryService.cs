using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Random;
using Moongate.Core.Utils;
using Moongate.Persistence.Services;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Builds mobiles from <see cref="IMobileTemplateService" />, dresses them through <see cref="IItemFactoryService" />
///     and writes them to the world database; the database gives each new mobile a serial in the mobile range.
/// </summary>
public class MobileFactoryService : IMobileFactoryService
{
    private const string GenderNameList = "{gender}";

    private readonly IMobileTemplateService _templates;
    private readonly INameService _names;
    private readonly IDataLoaderService _dataLoaderService;
    private readonly IItemFactoryService _itemFactory;
    private readonly IItemTemplateService _itemTemplates;
    private readonly ITileDataService _tiles;
    private readonly IMapService _maps;
    private readonly IMoongateEventBus _eventBus;
    private readonly MoongatePersistenceService _persistence;

    private readonly ILogger _logger = Log.ForContext<MobileFactoryService>();

    public MobileFactoryService(
        IMobileTemplateService templates,
        INameService names,
        IDataLoaderService dataLoaderService,
        IItemFactoryService itemFactory,
        IItemTemplateService itemTemplates,
        ITileDataService tiles,
        IMapService maps,
        IMoongateEventBus eventBus,
        MoongatePersistenceService persistence
    )
    {
        _templates = templates;
        _names = names;
        _dataLoaderService = dataLoaderService;
        _itemFactory = itemFactory;
        _itemTemplates = itemTemplates;
        _tiles = tiles;
        _maps = maps;
        _eventBus = eventBus;
        _persistence = persistence;
    }

    public MobileEntity Create(string templateId)
    {
        var template = _templates.Get(templateId);
        var gender = template.Gender switch
        {
            MobileGenderType.Male => GenderType.Male,
            MobileGenderType.Female => GenderType.Female,
            _ => BuiltInRng.Next(2) == 0 ? GenderType.Male : GenderType.Female
        };
        var race = template.Race is { } templateRace
            ? _dataLoaderService.GetEntities<RaceContent>().FirstOrDefault(content => content.Race == templateRace)
            : null;
        var looks = race?.For(gender);
        var strength = Roll(template.Strength);
        var dexterity = Roll(template.Dexterity);
        var intelligence = Roll(template.Intelligence);
        var hits = template.Hits?.Roll() ?? strength;
        var mana = template.Mana?.Roll() ?? intelligence;
        var stamina = template.Stamina?.Roll() ?? dexterity;
        var hairStyle = PickStyle(template.Hair ?? looks?.Hair);
        var beardStyle = gender == GenderType.Female ? 0 : PickStyle(template.Beard ?? looks?.Beard);

        return new MobileEntity
        {
            TemplateId = template.Id,
            Gender = gender,
            Race = template.Race ?? RaceType.Human,
            Body = template.Body ?? looks?.Body ?? 0,
            Name = template.Name ?? RandomName(template.NameList, gender),
            SkinHue = template.SkinHue?.Resolve() ?? PickHue(race?.SkinHues),
            HairStyle = hairStyle,
            HairHue = hairStyle == 0 ? default : template.HairHue?.Resolve() ?? PickHue(race?.HairHues),
            BeardStyle = beardStyle,
            BeardHue = beardStyle == 0 ? default : template.BeardHue?.Resolve() ?? PickHue(race?.HairHues),
            Strength = strength,
            Dexterity = dexterity,
            Intelligence = intelligence,
            Hits = hits,
            HitsMax = hits,
            Mana = mana,
            ManaMax = mana,
            Stamina = stamina,
            StaminaMax = stamina,
            Armor = Roll(template.Armor),
            ResistPhysical = Roll(template.Resistances?.Physical),
            ResistFire = Roll(template.Resistances?.Fire),
            ResistCold = Roll(template.Resistances?.Cold),
            ResistPoison = Roll(template.Resistances?.Poison),
            ResistEnergy = Roll(template.Resistances?.Energy),
            Fame = Roll(template.Fame),
            Karma = Roll(template.Karma),
            Skills = (template.Skills ?? []).Select(pair => RollSkill(template.Id, pair.Key, pair.Value)).ToList(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public Task<SpawnedMobile> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task SaveAsync(MobileEntity mobile, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    // Template skills are whole points; the mobile stores tenths (1000 is 100.0).
    private static MobileSkill RollSkill(string templateId, string name, DiceSpec points)
    {
        if (!EnumNameUtils.TryParse<SkillType>(name, out var skill))
        {
            throw new InvalidDataException($"Mobile template '{templateId}' has skill '{name}', which is not a skill.");
        }

        return new() { Skill = skill, Base = points.Roll() * 10 };
    }

    private string RandomName(string? nameList, GenderType gender)
    {
        return nameList switch
        {
            null => string.Empty,
            GenderNameList => _names.RandomName(gender == GenderType.Female ? "female" : "male"),
            _ => _names.RandomName(nameList)
        };
    }

    private static int Roll(DiceSpec? dice)
    {
        return dice?.Roll() ?? 0;
    }

    private static int PickStyle(List<int>? styles)
    {
        return styles is { Count: > 0 } ? styles[BuiltInRng.Next(styles.Count)] : 0;
    }

    private static Hue PickHue(List<HueSpec>? hues)
    {
        return hues is { Count: > 0 } ? hues[BuiltInRng.Next(hues.Count)].Resolve() : default;
    }
}
