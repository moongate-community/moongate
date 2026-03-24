using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Json.Teleporters;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads ModernUO-style teleporters JSON files from Data/teleporters.
/// </summary>
[RegisterFileLoader(23)]
public class TeleportersDataLoader : IFileLoader
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
    private readonly ITeleportersDataService _teleportersDataService;
    private readonly ILogger _logger = Log.ForContext<TeleportersDataLoader>();

    public TeleportersDataLoader(DirectoriesConfig directoriesConfig, ITeleportersDataService teleportersDataService)
    {
        _directoriesConfig = directoriesConfig;
        _teleportersDataService = teleportersDataService;
    }

    public Task LoadAsync()
    {
        var rootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Data], "teleporters");

        if (!Directory.Exists(rootDirectory))
        {
            _logger.Warning("Teleporters directory not found at {Path}.", rootDirectory);
            _teleportersDataService.SetEntries([]);

            return Task.CompletedTask;
        }

        var files = Directory.GetFiles(rootDirectory, "*.json", SearchOption.AllDirectories);
        var entries = new List<TeleporterEntry>();

        foreach (var filePath in files)
        {
            JsonTeleporterDefinition[] teleporters;

            try
            {
                teleporters = JsonUtils.DeserializeFromFile<JsonTeleporterDefinition[]>(
                    filePath,
                    MoongateUOJsonSerializationContext.Default
                );
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Skipping teleporter file {Path}: invalid JSON payload.", filePath);

                continue;
            }

            var importedFromFile = 0;

            foreach (var teleporter in teleporters)
            {
                if (!TryResolveMapId(teleporter.Src.Map, out var srcMapId) ||
                    !TryResolveMapId(teleporter.Dst.Map, out var dstMapId))
                {
                    _logger.Warning(
                        "Skipping teleporter in {FilePath}: unsupported src/dst map '{SrcMap}' -> '{DstMap}'.",
                        filePath,
                        teleporter.Src.Map,
                        teleporter.Dst.Map
                    );

                    continue;
                }

                if (!TryParsePoint3D(teleporter.Src.Loc, out var sourceLocation) ||
                    !TryParsePoint3D(teleporter.Dst.Loc, out var destinationLocation))
                {
                    _logger.Warning("Skipping teleporter in {FilePath}: invalid src/dst location payload.", filePath);

                    continue;
                }

                entries.Add(
                    new(
                        srcMapId,
                        teleporter.Src.Map.Trim(),
                        sourceLocation,
                        dstMapId,
                        teleporter.Dst.Map.Trim(),
                        destinationLocation,
                        teleporter.Back
                    )
                );
                importedFromFile++;
            }

            _logger.Information(
                "Loaded {Count} teleporters from file {File}.",
                importedFromFile,
                Path.GetFileName(filePath)
            );
        }

        _teleportersDataService.SetEntries(entries);
        _logger.Information("Loaded {Count} total teleporter definitions from {Path}.", entries.Count, rootDirectory);

        return Task.CompletedTask;
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

    private static bool TryResolveMapId(string mapName, out int mapId)
        => MapIdByName.TryGetValue(mapName.Trim(), out mapId);
}
