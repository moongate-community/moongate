using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads <c>data/starting_items.toml</c>. A set with no items, a set that is not common and has no filter, an entry
///     with no items, an item that names no template or an amount that can roll below 1 stops the server at startup.
/// </summary>
public class StartingItemsLoader : IDataLoader<StartingItemSet>
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private readonly ILogger _logger = Log.ForContext<StartingItemsLoader>();

    private string startingItemsFilePath => Path.Join(_directoriesConfig["data"], "starting_items.toml");

    public StartingItemsLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(startingItemsFilePath))
        {
            throw new FileNotFoundException("Starting items file starting_items.toml not found", startingItemsFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<StartingItemSet>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var file = await TomlUtils.DeserializeFromFileAsync<StartingItemsFile>(startingItemsFilePath, null, cancellationToken) ??
                   new StartingItemsFile();
        var templateIds = _dataLoaderService.GetEntities<ItemTemplate>()
                                            .Select(template => template.Id)
                                            .ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < file.Set.Count; i++)
        {
            var set = file.Set[i];
            var where = $"{startingItemsFilePath}: set {i + 1}";

            if (set.Items.Count == 0)
            {
                throw new InvalidDataException($"{where} has no items.");
            }

            if (!set.Common && set.Skill is null && set.Race is null && set.Gender is null)
            {
                throw new InvalidDataException($"{where} is not common and has no skill, race or gender, so no one gets it.");
            }

            foreach (var entry in set.Items)
            {
                if (entry.Items.Count == 0)
                {
                    throw new InvalidDataException($"{where} has an entry with no items.");
                }

                if (entry.Items.FirstOrDefault(item => !templateIds.Contains(item)) is { } missing)
                {
                    throw new InvalidDataException($"{where} gives item '{missing}', which is not an item template.");
                }

                if (entry.Amount is { } amount && amount.Min < 1)
                {
                    throw new InvalidDataException($"{where} has amount '{amount}', which can roll below 1.");
                }
            }
        }

        _logger.Information("Found {Count} starting item sets", file.Set.Count);

        return new() { Entities = file.Set };
    }
}
