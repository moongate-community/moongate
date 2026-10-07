using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads every <c>*.toml</c> under <c>templates/npc_lists/</c>, recursively, after the mobile templates. An empty
///     or
///     duplicate id, a list without entries, an entry naming both or neither a mobile and a list, a weight below 1, a
///     mobile template or list that does not exist, or lists that loop stop the server at startup.
/// </summary>
public class NpcListsLoader : IDataLoader<NpcListTemplate>
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private readonly ILogger _logger = Log.ForContext<NpcListsLoader>();

    private string listsDirectoryPath => Path.Join(_directoriesConfig["templates"], "npc_lists");

    public NpcListsLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<NpcListTemplate>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var byId = new Dictionary<string, (NpcListTemplate List, string File)>(StringComparer.Ordinal);

        if (Directory.Exists(listsDirectoryPath))
        {
            foreach (var path in Directory.EnumerateFiles(listsDirectoryPath, "*.toml", SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                var file = await TomlUtils.DeserializeFromFileAsync<NpcListTemplateFile>(path, null, cancellationToken) ??
                           new NpcListTemplateFile();

                foreach (var list in file.NpcList)
                {
                    if (string.IsNullOrWhiteSpace(list.Id))
                    {
                        throw new InvalidDataException($"{path}: an npc list has no id.");
                    }

                    if (!byId.TryAdd(list.Id, (list, path)))
                    {
                        throw new InvalidDataException(
                            $"Npc list '{list.Id}' is defined twice: in {byId[list.Id].File} and {path}."
                        );
                    }
                }
            }
        }

        var mobileIds = _dataLoaderService.GetEntities<MobileTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (list, file) in byId.Values)
        {
            Check(list, file, byId, mobileIds);
        }

        foreach (var id in byId.Keys)
        {
            CheckNoLoop(id, byId, [], []);
        }

        _logger.Information("Found {Count} npc lists", byId.Count);

        return new() { Entities = byId.Values.Select(pair => pair.List).ToList() };
    }

    private static void Check(
        NpcListTemplate list,
        string file,
        Dictionary<string, (NpcListTemplate List, string File)> byId,
        HashSet<string> mobileIds
    )
    {
        var where = $"{file}: npc list '{list.Id}'";

        if (list.Entries.Count == 0)
        {
            throw new InvalidDataException($"{where} has no entries.");
        }

        foreach (var entry in list.Entries)
        {
            if (entry.MobileId is not null && entry.NpcListId is not null)
            {
                throw new InvalidDataException($"{where} has an entry with both mobile_id and npc_list_id.");
            }

            if (entry.MobileId is null && entry.NpcListId is null)
            {
                throw new InvalidDataException($"{where} has an entry with neither mobile_id nor npc_list_id.");
            }

            if (entry.Weight < 1)
            {
                throw new InvalidDataException($"{where} has a weight below 1.");
            }

            if (entry.MobileId is { } mobile && !mobileIds.Contains(mobile))
            {
                throw new InvalidDataException($"{where} names '{mobile}', which is not a mobile template.");
            }

            if (entry.NpcListId is { } nested && !byId.ContainsKey(nested))
            {
                throw new InvalidDataException($"{where} nests '{nested}', which does not exist.");
            }
        }
    }

    private static void CheckNoLoop(
        string id,
        Dictionary<string, (NpcListTemplate List, string File)> byId,
        HashSet<string> visiting,
        HashSet<string> done
    )
    {
        if (done.Contains(id))
        {
            return;
        }

        if (!visiting.Add(id))
        {
            throw new InvalidDataException($"Npc list '{id}' nests itself through npc_list_id.");
        }

        foreach (var nested in byId[id].List.Entries.Select(entry => entry.NpcListId).OfType<string>())
        {
            CheckNoLoop(nested, byId, visiting, done);
        }

        visiting.Remove(id);
        done.Add(id);
    }
}
