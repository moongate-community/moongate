using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Random;
using Moongate.Core.Utils;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
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

    // MobileTemplate: "Unset is 10" for strength, dexterity and intelligence.
    private const int DefaultStat = 10;

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
        return Create(_templates.Get(templateId));
    }

    private MobileEntity Create(MobileTemplate template)
    {
        var gender = template.Gender switch
        {
            MobileGenderType.Female => GenderType.Female,
            MobileGenderType.Random => BuiltInRng.Next(2) == 0 ? GenderType.Male : GenderType.Female,
            _ => GenderType.Male
        };
        var race = template.Race is { } templateRace
            ? _dataLoaderService.GetEntities<RaceContent>().FirstOrDefault(content => content.Race == templateRace)
            : null;
        var looks = race?.For(gender);
        var strength = template.Strength?.Roll() ?? DefaultStat;
        var dexterity = template.Dexterity?.Roll() ?? DefaultStat;
        var intelligence = template.Intelligence?.Roll() ?? DefaultStat;
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

    public async Task<SpawnedMobile> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        CancellationToken cancellationToken = default
    )
    {
        // Checked before anything is built, so an out-of-map spawn fires no event.
        if (!_maps.Contains(map, location.X, location.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(location), location, $"{location} is outside {map}.");
        }

        var template = _templates.Get(templateId);
        var mobile = Create(template);

        if (mobile.Body == 0)
        {
            throw new InvalidDataException(
                $"Mobile template '{templateId}' has no body and no race, so it cannot be spawned."
            );
        }

        mobile.Map = map;
        mobile.Location = location;
        await _eventBus.PublishAsync(new MobileBeforeSpawnEvent(mobile, map, location), cancellationToken);

        // A handler may have moved the mobile: it must still be on its map.
        if (!_maps.Contains(mobile.Map, mobile.Location.X, mobile.Location.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(location),
                mobile.Location,
                $"A MobileBeforeSpawnEvent handler moved the mobile to {mobile.Location}, outside {mobile.Map}."
            );
        }

        var equipment = new List<ItemEntity>();

        try
        {
            await SaveSpawnAsync(template, mobile, equipment, cancellationToken);
        }
        catch
        {
            // The rollback undid the rows; the in-memory mobile must not keep a serial that does not exist.
            mobile.Id = Serial.Zero;

            throw;
        }

        var spawned = new SpawnedMobile(mobile, equipment);

        // After the commit the mobile is saved whatever happens: the events are published without cancellation.
        await _eventBus.PublishAsync(new MobileMovedToWorldEvent(mobile, mobile.Map, mobile.Location), CancellationToken.None);
        await _eventBus.PublishAsync(new MobileAfterSpawnEvent(spawned), CancellationToken.None);

        return spawned;
    }

    private Task SaveSpawnAsync(
        MobileTemplate template,
        MobileEntity mobile,
        List<ItemEntity> equipment,
        CancellationToken cancellationToken
    )
    {
        var templateId = template.Id;

        return _persistence.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async transaction =>
            {
                await transaction.GetDataAccess<MobileEntity>().UpsertAsync(mobile, cancellationToken);

                if (!mobile.Id.IsMobile)
                {
                    throw new InvalidOperationException(
                        $"Mobile '{templateId}' was saved with {mobile.Id}, outside the mobile range."
                    );
                }

                var usedLayers = new HashSet<LayerType>();

                foreach (var entry in template.Equipment ?? [])
                {
                    if (entry.Gender is { } gender && gender != mobile.Gender)
                    {
                        continue;
                    }

                    var itemId = entry.Items[BuiltInRng.Next(entry.Items.Count)];
                    var item = _itemFactory.Create(itemId, hue: entry.Hue?.Resolve());
                    var layer = _itemTemplates.Get(itemId).EffectiveLayer(_tiles);

                    // NPCs have no backpack yet, so what cannot be worn is dropped.
                    if (layer is null || !usedLayers.Add(layer.Value))
                    {
                        _logger.Debug("Mobile {TemplateId} drops {ItemId}: no free layer", templateId, itemId);

                        continue;
                    }

                    item.Equip(mobile.Id, layer.Value);
                    await _itemFactory.SaveAsync(transaction, item, cancellationToken);
                    equipment.Add(item);
                }
            },
            cancellationToken
        );
    }

    public Task SaveAsync(MobileEntity mobile, CancellationToken cancellationToken = default)
    {
        if (mobile.Id == Serial.Zero)
        {
            throw new InvalidOperationException(
                $"Mobile '{mobile.TemplateId}' has no serial yet: spawn it with SpawnAsync before saving it."
            );
        }

        return _persistence.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            transaction => transaction.GetDataAccess<MobileEntity>().UpsertAsync(mobile, cancellationToken),
            cancellationToken
        );
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
