using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.StartingItems;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads the starting-items table into <see cref="IStartingItemsService" /> at startup: seeds the
/// embedded <c>starting_items.yaml</c> into the data directory if missing, then parses and loads it.
/// </summary>
public sealed class StartingItemsLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<StartingItemsLoader>();
    private readonly IStartingItemsService _startingItems;
    private readonly DirectoriesConfig _directories;

    public StartingItemsLoader(IStartingItemsService startingItems, DirectoriesConfig directories)
    {
        _startingItems = startingItems;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "starting_items.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(
                typeof(StartingItemsLoader).Assembly,
                "Assets/starting_items.yaml"
            );
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default starting_items.yaml at {Path}", path);
        }

        var data = YamlUtils.DeserializeFromFile<StartingItemsData>(path) ?? new StartingItemsData();
        _startingItems.Load(data);

        _logger.Information(
            "Loaded starting items: {Bodies} body kits, {Skills} skill kits from {Path}",
            data.ByBody.Count,
            data.BySkill.Count,
            path
        );

        return ValueTask.CompletedTask;
    }
}
