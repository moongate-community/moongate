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
/// Loads signs.cfg using the same map-code semantics as ModernUO SignParser.
/// </summary>
[RegisterFileLoader(21)]
public class SignDataLoader : IFileLoader
{
    private static readonly IReadOnlyDictionary<int, int[]> MapIdsBySourceCode =
        new Dictionary<int, int[]>
        {
            [0] = [0, 1], // britannia => felucca + trammel
            [1] = [0],    // felucca
            [2] = [1],    // trammel
            [3] = [2],    // ilshenar
            [4] = [3],    // malas
            [5] = [4]     // tokuno
        };

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ISignDataService _signDataService;
    private readonly ILogger _logger = Log.ForContext<SignDataLoader>();

    public SignDataLoader(DirectoriesConfig directoriesConfig, ISignDataService signDataService)
    {
        _directoriesConfig = directoriesConfig;
        _signDataService = signDataService;
    }

    public async Task LoadAsync()
    {
        var signsFile = Path.Combine(_directoriesConfig[DirectoryType.Data], "signs", "signs.cfg");

        if (!File.Exists(signsFile))
        {
            _logger.Warning("Signs file not found at {Path}.", signsFile);
            _signDataService.SetEntries([]);

            return;
        }

        var entries = new List<SignEntry>();

        using var reader = new StreamReader(signsFile);

        while (await reader.ReadLineAsync() is { } line)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!TryParseLine(trimmed, out var parsed))
            {
                continue;
            }

            if (!MapIdsBySourceCode.TryGetValue(parsed.SourceMapCode, out var mapIds))
            {
                continue;
            }

            foreach (var mapId in mapIds)
            {
                entries.Add(
                    new(
                        mapId,
                        parsed.SourceMapCode,
                        parsed.ItemId,
                        parsed.Location,
                        parsed.Text
                    )
                );
            }
        }

        _signDataService.SetEntries(entries);
        _logger.Information("Loaded {Count} sign entries from {Path}.", entries.Count, signsFile);
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

    private static bool TryParseLine(
        string line,
        out (int SourceMapCode, Serial ItemId, Point3D Location, string Text) parsed
    )
    {
        var tokens = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < 6)
        {
            parsed = default;

            return false;
        }

        var sourceMapCode = ParseInt(tokens[0]);
        var itemIdValue = ParseInt(tokens[1]);
        var x = ParseInt(tokens[2]);
        var y = ParseInt(tokens[3]);
        var z = ParseInt(tokens[4]);
        var text = tokens[5].Trim();
        var itemId = itemIdValue <= 0 ? Serial.Zero : (Serial)(uint)itemIdValue;

        parsed = (sourceMapCode, itemId, new(x, y, z), text);

        return true;
    }
}
