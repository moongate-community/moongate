using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Teleporters;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads teleporters into <see cref="ITeleporterService" /> at startup: seeds the embedded
/// <c>teleporters.yaml</c> into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class TeleportersLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<TeleportersLoader>();
    private readonly ITeleporterService _teleporters;
    private readonly DirectoriesConfig _directories;

    public TeleportersLoader(ITeleporterService teleporters, DirectoriesConfig directories)
    {
        _teleporters = teleporters;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "teleporters.yaml");

        if (!File.Exists(path))
        {
            var seed =
                ResourceUtils.GetEmbeddedResourceString(typeof(TeleportersLoader).Assembly, "Assets/teleporters.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default teleporters.yaml at {Path}", path);
        }

        var teleporters = YamlUtils.DeserializeFromFile<TeleporterDefinition[]>(path) ?? [];

        foreach (var teleporter in teleporters)
        {
            _teleporters.Register(teleporter);
        }

        _logger.Information("Loaded {Count} teleporter(s) from {Path}", teleporters.Length, path);

        return ValueTask.CompletedTask;
    }
}
