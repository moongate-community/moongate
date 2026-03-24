using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Json.Spawns;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads ModernUO-style spawn JSON files from Data/spawns.
/// </summary>
[RegisterFileLoader(22)]
public class SpawnsDataLoader : IFileLoader
{
    private static readonly IReadOnlyDictionary<string, int> MapIdByName =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["felucca"] = 0,
            ["trammel"] = 1,
            ["ilshenar"] = 2,
            ["malas"] = 3,
            ["tokuno"] = 4,
            ["termur"] = 5,
            ["internal"] = 0x7F
        };

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ISpawnsDataService _spawnsDataService;
    private readonly ILogger _logger = Log.ForContext<SpawnsDataLoader>();

    public SpawnsDataLoader(DirectoriesConfig directoriesConfig, ISpawnsDataService spawnsDataService)
    {
        _directoriesConfig = directoriesConfig;
        _spawnsDataService = spawnsDataService;
    }

    public Task LoadAsync()
    {
        var rootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Data], "spawns");

        if (!Directory.Exists(rootDirectory))
        {
            _logger.Warning("Spawns directory not found at {Path}.", rootDirectory);
            _spawnsDataService.SetEntries([]);

            return Task.CompletedTask;
        }

        var files = Directory.GetFiles(rootDirectory, "*.json", SearchOption.AllDirectories);
        var entries = new List<SpawnDefinitionEntry>();

        foreach (var filePath in files)
        {
            JsonSpawnDefinition[] spawns;

            try
            {
                spawns = JsonUtils.DeserializeFromFile<JsonSpawnDefinition[]>(
                    filePath,
                    MoongateUOJsonSerializationContext.Default
                );
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Skipping spawn file {Path}: invalid JSON payload.", filePath);

                continue;
            }

            var sourceGroup = ResolveSourceGroup(rootDirectory, filePath);
            var sourceFile = Path.GetFileName(filePath);
            var importedFromFile = 0;

            foreach (var spawn in spawns)
            {
                if (!TryResolveMap(spawn, sourceGroup, out var mapId, out var mapName))
                {
                    _logger.Warning(
                        "Skipping spawn {SpawnName} in {FilePath}: unsupported map '{MapName}'.",
                        spawn.Name,
                        filePath,
                        spawn.Map
                    );

                    continue;
                }

                if (!TryParsePoint3D(spawn.Location, out var location))
                {
                    _logger.Warning(
                        "Skipping spawn {SpawnName} in {FilePath}: invalid location payload.",
                        spawn.Name,
                        filePath
                    );

                    continue;
                }

                entries.Add(
                    new(
                        mapId,
                        mapName,
                        sourceGroup,
                        sourceFile,
                        spawn.Guid,
                        ResolveKind(spawn.Type),
                        spawn.Name,
                        location,
                        spawn.Count,
                        spawn.MinDelay,
                        spawn.MaxDelay,
                        spawn.Team,
                        spawn.HomeRange,
                        spawn.WalkingRange,
                        [
                            ..spawn.Entries.Select(
                                static item => new SpawnEntryDefinition(item.Name, item.MaxCount, item.Probability)
                            )
                        ]
                    )
                );
                importedFromFile++;
            }

            _logger.Information("Loaded {Count} spawns from file {File}.", importedFromFile, sourceFile);
        }

        _spawnsDataService.SetEntries(entries);
        _logger.Information("Loaded {Count} total spawn definitions from {Path}.", entries.Count, rootDirectory);

        return Task.CompletedTask;
    }

    private static SpawnDefinitionKind ResolveKind(string? rawType)
        => string.Equals(rawType, "ProximitySpawner", StringComparison.OrdinalIgnoreCase)
               ? SpawnDefinitionKind.ProximitySpawner
               : SpawnDefinitionKind.Spawner;

    private static string ResolveSourceGroup(string rootDirectory, string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath) ?? rootDirectory;
        var relative = Path.GetRelativePath(rootDirectory, directoryPath);

        return relative == "." ? string.Empty : relative.Replace('\\', '/');
    }

    private static bool TryParsePoint3D(int[] value, out Point3D location)
    {
        if (value.Length < 3)
        {
            location = Point3D.Zero;

            return false;
        }

        location = new(value[0], value[1], value[2]);

        return true;
    }

    private static bool TryResolveMap(
        JsonSpawnDefinition spawn,
        string sourceGroup,
        out int mapId,
        out string mapName
    )
    {
        var resolvedMapName = spawn.Map.Trim();

        if (resolvedMapName.Length == 0 && sourceGroup.Length > 0)
        {
            resolvedMapName = sourceGroup.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        }

        if (MapIdByName.TryGetValue(resolvedMapName, out mapId))
        {
            mapName = resolvedMapName;

            return true;
        }

        mapName = resolvedMapName;

        return false;
    }
}
