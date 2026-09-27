using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads every <c>*.toml</c> under <c>templates/mobiles/</c>, recursively, and resolves <c>base_id</c> as documented
///     on <see cref="MobileTemplate" />: unset fields come from the parent; skills, resistances and sounds key by key;
///     tags merge; equipment and loot, when set, replace. Inherited values are copies. A duplicate or missing id, a
///     cycle, an invalid template, an unknown name list or an equipment item that is not an item template stops the
///     server at startup.
/// </summary>
public class MobileTemplatesLoader : IDataLoader<MobileTemplate>
{
    private const string GenderNameList = "{gender}";

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private readonly ILogger _logger = Log.ForContext<MobileTemplatesLoader>();

    private string mobilesDirectoryPath => Path.Join(_directoriesConfig["templates"], "mobiles");

    public MobileTemplatesLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<MobileTemplate>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var byId = new Dictionary<string, (MobileTemplate Template, string File)>(StringComparer.Ordinal);

        if (Directory.Exists(mobilesDirectoryPath))
        {
            foreach (var path in Directory.EnumerateFiles(mobilesDirectoryPath, "*.toml", SearchOption.AllDirectories)
                                          .Order(StringComparer.Ordinal))
            {
                var file = await TomlUtils.DeserializeFromFileAsync<MobileTemplateFile>(path, null, cancellationToken) ??
                           new MobileTemplateFile();

                foreach (var template in file.Mobile)
                {
                    if (string.IsNullOrWhiteSpace(template.Id))
                    {
                        throw new InvalidDataException($"{path}: a mobile template has no id.");
                    }

                    if (!byId.TryAdd(template.Id, (template, path)))
                    {
                        throw new InvalidDataException(
                            $"Mobile template id '{template.Id}' is defined in both {byId[template.Id].File} and {path}."
                        );
                    }
                }
            }
        }

        var resolved = new Dictionary<string, MobileTemplate>(StringComparer.Ordinal);

        foreach (var id in byId.Keys)
        {
            Resolve(id, byId, resolved, []);
        }

        var nameLists = _dataLoaderService.GetEntities<NameList>()
                                          .Select(list => list.Id)
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var itemIds = _dataLoaderService.GetEntities<ItemTemplate>()
                                        .Select(template => template.Id)
                                        .ToHashSet(StringComparer.Ordinal);

        foreach (var template in resolved.Values)
        {
            template.Validate();

            if (template.NameList is { } list && list != GenderNameList && !nameLists.Contains(list))
            {
                throw new InvalidDataException($"Mobile template '{template.Id}' has name_list '{list}', which does not exist.");
            }

            if (template.Equipment?.SelectMany(entry => entry.Items).FirstOrDefault(item => !itemIds.Contains(item)) is
                { } missing)
            {
                throw new InvalidDataException(
                    $"Mobile template '{template.Id}' equips '{missing}', which is not an item template."
                );
            }
        }

        _logger.Information("Found {Count} mobile templates", resolved.Count);

        return new() { Entities = byId.Keys.Select(id => resolved[id]).ToList() };
    }

    private static MobileTemplate Resolve(
        string id,
        Dictionary<string, (MobileTemplate Template, string File)> byId,
        Dictionary<string, MobileTemplate> resolved,
        HashSet<string> visiting
    )
    {
        if (resolved.TryGetValue(id, out var done))
        {
            return done;
        }

        if (!visiting.Add(id))
        {
            throw new InvalidDataException($"Mobile template '{id}' inherits from itself through base_id.");
        }

        var (template, file) = byId[id];

        if (template.BaseId is { } baseId)
        {
            if (!byId.ContainsKey(baseId))
            {
                throw new InvalidDataException($"{file}: mobile template '{id}' has base_id '{baseId}', which does not exist.");
            }

            Inherit(template, Resolve(baseId, byId, resolved, visiting));
        }

        visiting.Remove(id);
        resolved[id] = template;

        return template;
    }

    private static void Inherit(MobileTemplate child, MobileTemplate parent)
    {
        child.Name ??= parent.Name;
        child.NameList ??= parent.NameList;
        child.Title ??= parent.Title;
        child.Body ??= parent.Body;
        child.Gender ??= parent.Gender;
        child.Race ??= parent.Race;
        child.SkinHue ??= parent.SkinHue;
        child.Hair ??= parent.Hair is null ? null : [..parent.Hair];
        child.HairHue ??= parent.HairHue;
        child.Beard ??= parent.Beard is null ? null : [..parent.Beard];
        child.BeardHue ??= parent.BeardHue;
        child.Strength ??= parent.Strength;
        child.Dexterity ??= parent.Dexterity;
        child.Intelligence ??= parent.Intelligence;
        child.Hits ??= parent.Hits;
        child.Mana ??= parent.Mana;
        child.Stamina ??= parent.Stamina;
        child.Damage ??= parent.Damage;
        child.Armor ??= parent.Armor;
        child.Notoriety ??= parent.Notoriety;
        child.Karma ??= parent.Karma;
        child.Fame ??= parent.Fame;
        child.Gold ??= parent.Gold;
        child.ScriptId ??= parent.ScriptId;
        child.Visibility ??= parent.Visibility;
        child.Loot ??= parent.Loot is null ? null : [..parent.Loot];
        child.Equipment ??= parent.Equipment?.Select(
                                                entry => new MobileEquipmentEntry
                                                    { Items = [..entry.Items], Hue = entry.Hue, Gender = entry.Gender }
                                            )
                                            .ToList();

        if (parent.Skills is not null)
        {
            child.Skills ??= new();

            foreach (var (skill, dice) in parent.Skills)
            {
                child.Skills.TryAdd(skill, dice);
            }
        }

        if (parent.Tags is not null)
        {
            child.Tags ??= new();

            foreach (var (key, value) in parent.Tags)
            {
                child.Tags.TryAdd(key, value);
            }
        }

        if (parent.Resistances is { } resistances)
        {
            child.Resistances ??= new();
            child.Resistances.Physical ??= resistances.Physical;
            child.Resistances.Fire ??= resistances.Fire;
            child.Resistances.Cold ??= resistances.Cold;
            child.Resistances.Poison ??= resistances.Poison;
            child.Resistances.Energy ??= resistances.Energy;
        }

        if (parent.Sounds is { } sounds)
        {
            child.Sounds ??= new();
            child.Sounds.StartAttack ??= sounds.StartAttack;
            child.Sounds.Idle ??= sounds.Idle;
            child.Sounds.Attack ??= sounds.Attack;
            child.Sounds.Hurt ??= sounds.Hurt;
            child.Sounds.Death ??= sounds.Death;
        }
    }
}
