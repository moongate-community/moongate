using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the spawns of every <c>templates/spawns/&lt;map&gt;/*.toml</c>, after the mobile templates and the npc
///     lists; the folder is the map. A folder that is not a map, an id used twice, nothing to spawn, an unknown mobile
///     template or npc list, a maximum or call below 1, times below 0 or a minimum above the maximum, or no area or an
///     area whose corners are reversed stop the server at startup.
/// </summary>
public class SpawnsLoader : IDataLoader<SpawnTemplate>
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private readonly ILogger _logger = Log.ForContext<SpawnsLoader>();

    private string spawnsDirectoryPath => Path.Join(_directoriesConfig["templates"], "spawns");

    public SpawnsLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<SpawnTemplate>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var spawns = new List<SpawnTemplate>();

        if (!Directory.Exists(spawnsDirectoryPath))
        {
            return new() { Entities = spawns };
        }

        var mobileIds = _dataLoaderService.GetEntities<MobileTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var listIds = _dataLoaderService.GetEntities<NpcListTemplate>()
            .Select(list => list.Id)
            .ToHashSet(StringComparer.Ordinal);
        var itemIds = _dataLoaderService.GetEntities<ItemTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var folder in Directory.GetDirectories(spawnsDirectoryPath).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(folder);

            if (!name.All(char.IsLower) || !Enum.TryParse<MapType>(name, true, out var map))
            {
                throw new InvalidDataException($"{folder}: spawn folder '{name}' is not a map, such as felucca.");
            }

            foreach (var path in Directory.EnumerateFiles(folder, "*.toml", SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                var file = await TomlUtils.DeserializeFromFileAsync<SpawnTemplateFile>(path, null, cancellationToken) ??
                           new SpawnTemplateFile();

                foreach (var spawn in file.Spawn)
                {
                    spawn.Map = map;

                    if (string.IsNullOrWhiteSpace(spawn.Id))
                    {
                        throw new InvalidDataException($"{path}: a spawn has no id.");
                    }

                    if (!files.TryAdd(spawn.Id, path))
                    {
                        throw new InvalidDataException(
                            $"Spawn '{spawn.Id}' is defined twice: in {files[spawn.Id]} and {path}."
                        );
                    }

                    Check(spawn, path, mobileIds, listIds, itemIds);
                    spawns.Add(spawn);
                }
            }
        }

        _logger.Information(
            "Found {Count} spawns for up to {Npcs} NPCs and {Items} items",
            spawns.Count,
            spawns.Where(spawn => spawn.ItemIds.Count == 0).Sum(spawn => spawn.Max),
            spawns.Where(spawn => spawn.ItemIds.Count > 0).Sum(spawn => spawn.Max)
        );

        return new() { Entities = spawns };
    }

    private static void Check(
        SpawnTemplate spawn,
        string file,
        HashSet<string> mobileIds,
        HashSet<string> listIds,
        HashSet<string> itemIds
    )
    {
        var where = $"{file}: spawn '{spawn.Id}'";
        var npcs = spawn.MobileIds.Count > 0 || spawn.NpcListIds.Count > 0;

        if (!npcs && spawn.ItemIds.Count == 0)
        {
            throw new InvalidDataException($"{where} has nothing to spawn: give mobile_ids, npc_list_ids or item_ids.");
        }

        if (npcs && spawn.ItemIds.Count > 0)
        {
            throw new InvalidDataException($"{where} has both NPCs and item_ids: a spawn is of NPCs or of items.");
        }

        foreach (var item in spawn.ItemIds.Where(item => !itemIds.Contains(item)))
        {
            throw new InvalidDataException($"{where} spawns '{item}', which is not an item template.");
        }

        foreach (var mobile in spawn.MobileIds.Where(mobile => !mobileIds.Contains(mobile)))
        {
            throw new InvalidDataException($"{where} spawns '{mobile}', which is not a mobile template.");
        }

        foreach (var list in spawn.NpcListIds.Where(list => !listIds.Contains(list)))
        {
            throw new InvalidDataException($"{where} spawns from '{list}', which is not an npc list.");
        }

        if (spawn.Max < 1 || spawn.Call < 1)
        {
            throw new InvalidDataException($"{where} needs a max and a call of at least 1.");
        }

        if (spawn.MinMinutes < 0 || spawn.MinMinutes > spawn.MaxMinutes)
        {
            throw new InvalidDataException($"{where} needs 0 <= min_minutes <= max_minutes.");
        }

        if (spawn.Areas.Count == 0)
        {
            throw new InvalidDataException($"{where} has no area.");
        }

        if (spawn.Areas.Concat(spawn.Exclude).Any(area => area.X1 > area.X2 || area.Y1 > area.Y2))
        {
            throw new InvalidDataException($"{where} has an area whose first corner is past its second.");
        }
    }
}
