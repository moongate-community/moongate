using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads every <c>*.toml</c> under <c>templates/loots/</c>, recursively; a table with no entries is kept, logged,
///     and gives nothing. An empty or duplicate id, an entry with both an item and a nested table, an item that is not an item template, a nested table
///     that does not exist, a weight below 1, an amount that can roll below 1 or nested tables that loop stop the
///     server at startup.
/// </summary>
public class LootTemplatesLoader : IDataLoader<LootTemplate>
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private readonly ILogger _logger = Log.ForContext<LootTemplatesLoader>();

    private string lootsDirectoryPath => Path.Join(_directoriesConfig["templates"], "loots");

    public LootTemplatesLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<LootTemplate>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var byId = new Dictionary<string, (LootTemplate Table, string File)>(StringComparer.Ordinal);

        if (Directory.Exists(lootsDirectoryPath))
        {
            foreach (var path in Directory.EnumerateFiles(lootsDirectoryPath, "*.toml", SearchOption.AllDirectories)
                                          .Order(StringComparer.Ordinal))
            {
                var file = await TomlUtils.DeserializeFromFileAsync<LootTemplateFile>(path, null, cancellationToken) ??
                           new LootTemplateFile();

                foreach (var table in file.Loot)
                {
                    if (string.IsNullOrWhiteSpace(table.Id))
                    {
                        throw new InvalidDataException($"{path}: a loot table has no id.");
                    }

                    if (!byId.TryAdd(table.Id, (table, path)))
                    {
                        throw new InvalidDataException(
                            $"Loot table id '{table.Id}' is defined in both {byId[table.Id].File} and {path}."
                        );
                    }
                }
            }
        }

        var itemIds = _dataLoaderService.GetEntities<ItemTemplate>()
                                        .Select(template => template.Id)
                                        .ToHashSet(StringComparer.Ordinal);

        foreach (var (table, file) in byId.Values)
        {
            Check(table, file, byId, itemIds);
        }

        var done = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in byId.Keys)
        {
            CheckNoCycle(id, byId, [], done);
        }

        // UOX3 tables whose items the converter could not resolve come out empty; rolling one gives nothing.
        var empty = byId.Values.Where(pair => pair.Table.Entries.Count == 0).Select(pair => pair.Table.Id).ToList();

        if (empty.Count > 0)
        {
            _logger.Warning("{Count} loot tables have no entries and give nothing: {Tables}", empty.Count, empty);
        }

        _logger.Information("Found {Count} loot tables", byId.Count);

        return new() { Entities = byId.Values.Select(pair => pair.Table).ToList() };
    }

    private static void Check(
        LootTemplate table,
        string file,
        Dictionary<string, (LootTemplate Table, string File)> byId,
        HashSet<string> itemIds
    )
    {
        var where = $"{file}: loot table '{table.Id}'";

        foreach (var entry in table.Entries)
        {
            if (entry.ItemId is not null && entry.LootTemplateId is not null)
            {
                throw new InvalidDataException($"{where} has an entry with both item_id and loot_template_id.");
            }

            if (entry.ItemId is { } itemId && !itemIds.Contains(itemId))
            {
                throw new InvalidDataException($"{where} drops '{itemId}', which is not an item template.");
            }

            if (entry.LootTemplateId is { } nested && !byId.ContainsKey(nested))
            {
                throw new InvalidDataException($"{where} nests '{nested}', which does not exist.");
            }

            if (entry.Weight < 1)
            {
                throw new InvalidDataException($"{where} has a weight below 1.");
            }

            if ((entry.ItemId is not null || entry.LootTemplateId is not null) &&
                (entry.Amount.Min < 1 || entry.Amount.Max > ushort.MaxValue))
            {
                throw new InvalidDataException($"{where} has an amount that can roll outside 1 to 65535.");
            }
        }
    }

    private static void CheckNoCycle(
        string id,
        Dictionary<string, (LootTemplate Table, string File)> byId,
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
            throw new InvalidDataException($"Loot table '{id}' nests itself through loot_template_id.");
        }

        foreach (var nested in byId[id].Table.Entries.Select(entry => entry.LootTemplateId).OfType<string>())
        {
            CheckNoCycle(nested, byId, visiting, done);
        }

        visiting.Remove(id);
        done.Add(id);
    }
}
