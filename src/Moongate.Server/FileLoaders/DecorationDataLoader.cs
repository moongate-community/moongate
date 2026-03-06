using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.FileLoaders;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads ModernUO-style decoration .cfg files from Data/decoration.
/// </summary>
[RegisterFileLoader(20)]
public class DecorationDataLoader : IFileLoader
{
    private static readonly IReadOnlyDictionary<string, int[]> MapIdsByFolderName =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Britannia"] = [0, 1],
            ["Felucca"] = [0],
            ["Trammel"] = [1],
            ["Ilshenar"] = [2],
            ["Malas"] = [3],
            ["Tokuno"] = [4],
            ["Termur"] = [5],
            ["RuinedMaginciaFel"] = [0],
            ["RuinedMaginciaTram"] = [1]
        };

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDecorationDataService _decorationDataService;
    private readonly ILogger _logger = Log.ForContext<DecorationDataLoader>();

    public DecorationDataLoader(DirectoriesConfig directoriesConfig, IDecorationDataService decorationDataService)
    {
        _directoriesConfig = directoriesConfig;
        _decorationDataService = decorationDataService;
    }

    public async Task LoadAsync()
    {
        var rootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Data], "decoration");

        if (!Directory.Exists(rootDirectory))
        {
            _logger.Warning("Decoration directory not found at {Path}.", rootDirectory);
            _decorationDataService.SetEntries([]);

            return;
        }

        var entries = new List<DecorationEntry>();
        var groupDirectories = Directory.GetDirectories(rootDirectory);

        foreach (var groupDirectory in groupDirectories)
        {
            var groupName = Path.GetFileName(groupDirectory);

            if (!MapIdsByFolderName.TryGetValue(groupName, out var mapIds))
            {
                _logger.Warning(
                    "Skipping decoration group '{GroupName}' because no map mapping is configured.",
                    groupName
                );

                continue;
            }

            var cfgFiles = Directory.GetFiles(groupDirectory, "*.cfg", SearchOption.TopDirectoryOnly);

            foreach (var cfgFile in cfgFiles)
            {
                var parsedEntries = await ParseFileAsync(cfgFile, groupName);

                foreach (var mapId in mapIds)
                {
                    entries.AddRange(
                        parsedEntries.Select(
                            parsedEntry => parsedEntry with
                            {
                                MapId = mapId
                            }
                        )
                    );
                }
            }
        }

        _decorationDataService.SetEntries(entries);
        _logger.Information("Loaded {Count} decoration entries from {Path}.", entries.Count, rootDirectory);
    }

    private async Task<List<DecorationEntry>> ParseFileAsync(string filePath, string groupName)
    {
        var entries = new List<DecorationEntry>();

        using var reader = new StreamReader(filePath);

        while (true)
        {
            var header = await ReadNextHeaderAsync(reader);

            if (header is null)
            {
                break;
            }

            var (typeName, itemId, parameters) = ParseHeader(header);
            var sourceFile = Path.GetFileName(filePath);

            foreach (var (location, extra) in await ReadBlockEntriesAsync(reader))
            {
                entries.Add(new(0, groupName, sourceFile, typeName, itemId, parameters, location, extra));
            }
        }

        return entries;
    }

    private static (string TypeName, Serial ItemId, IReadOnlyDictionary<string, string> Parameters) ParseHeader(
        string headerLine
    )
    {
        var firstSpace = headerLine.IndexOf(' ');

        if (firstSpace < 0)
        {
            return (headerLine, Serial.Zero, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var typeName = headerLine[..firstSpace].Trim();
        var span = headerLine[(firstSpace + 1)..].Trim();
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var itemPart = span;
        var argsStart = span.IndexOf('(');

        if (argsStart >= 0)
        {
            itemPart = span[..argsStart].Trim();
            var argsPart = span[(argsStart + 1)..];

            if (argsPart.EndsWith(')'))
            {
                argsPart = argsPart[..^1];
            }

            foreach (
                var rawToken in argsPart.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            {
                var equalsIndex = rawToken.IndexOf('=');

                if (equalsIndex <= 0)
                {
                    parameters[rawToken] = string.Empty;

                    continue;
                }

                var key = rawToken[..equalsIndex].Trim();
                var value = rawToken[(equalsIndex + 1)..].Trim();

                if (key.Length == 0)
                {
                    continue;
                }

                parameters[key] = value;
            }
        }

        return (typeName, ParseSerial(itemPart), parameters);
    }

    private static int ParseInt(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(trimmed[2..], 16);
        }

        return int.TryParse(trimmed, out var parsed) ? parsed : 0;
    }

    private static Serial ParseSerial(string value)
    {
        var parsed = ParseInt(value);

        return parsed <= 0 ? Serial.Zero : (Serial)(uint)parsed;
    }

    private static async Task<List<(Point3D Location, string Extra)>> ReadBlockEntriesAsync(StreamReader reader)
    {
        var entries = new List<(Point3D Location, string Extra)>();

        while (await reader.ReadLineAsync() is { } line)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                break;
            }

            if (trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!TryParsePlacement(trimmed, out var placement))
            {
                continue;
            }

            entries.Add(placement);
        }

        return entries;
    }

    private static async Task<string?> ReadNextHeaderAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            return trimmed;
        }

        return null;
    }

    private static bool TryParsePlacement(string line, out (Point3D Location, string Extra) placement)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < 3)
        {
            placement = default;

            return false;
        }

        var x = ParseInt(tokens[0]);
        var y = ParseInt(tokens[1]);
        var z = ParseInt(tokens[2]);
        var extra = tokens.Length > 3 ? string.Join(' ', tokens.Skip(3)) : string.Empty;

        placement = (new(x, y, z), extra);

        return true;
    }
}
