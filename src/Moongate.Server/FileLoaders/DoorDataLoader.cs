using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Interfaces.FileLoaders;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads ModernUO door components from data/components/doors.txt.
/// </summary>
[RegisterFileLoader(22)]
public class DoorDataLoader : IFileLoader
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDoorDataService _doorDataService;
    private readonly ILogger _logger = Log.ForContext<DoorDataLoader>();

    public DoorDataLoader(DirectoriesConfig directoriesConfig, IDoorDataService doorDataService)
    {
        _directoriesConfig = directoriesConfig;
        _doorDataService = doorDataService;
    }

    public async Task LoadAsync()
    {
        var filePath = Path.Combine(_directoriesConfig[DirectoryType.Data], "components", "doors.txt");

        if (!File.Exists(filePath))
        {
            _logger.Warning("Doors components file not found at {Path}.", filePath);
            _doorDataService.SetEntries([]);

            return;
        }

        var entries = new List<DoorComponentEntry>();
        var errors = new List<string>();

        using var reader = new StreamReader(filePath);
        var lineNumber = 0;
        while (await reader.ReadLineAsync() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!char.IsDigit(trimmed[0]))
            {
                continue;
            }

            if (!TryParseLine(trimmed, out var entry))
            {
                errors.Add($"Invalid doors.txt row at line {lineNumber}: '{line}'.");
                continue;
            }

            entries.Add(entry);
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                _logger.Error(error);
            }

            throw new InvalidOperationException($"Door data validation failed with {errors.Count} error(s).");
        }

        _doorDataService.SetEntries(entries);
        _logger.Information("Loaded {Count} door component entries from {Path}.", entries.Count, filePath);
    }

    private static bool TryParseLine(string line, out DoorComponentEntry entry)
    {
        var tokens = line.Split(
            separator: (char[]?)null,
            count: 11,
            options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        if (tokens.Length != 11)
        {
            entry = default;

            return false;
        }

        if (!TryParseInt(tokens[0], out var category) ||
            !TryParseInt(tokens[1], out var piece1) ||
            !TryParseInt(tokens[2], out var piece2) ||
            !TryParseInt(tokens[3], out var piece3) ||
            !TryParseInt(tokens[4], out var piece4) ||
            !TryParseInt(tokens[5], out var piece5) ||
            !TryParseInt(tokens[6], out var piece6) ||
            !TryParseInt(tokens[7], out var piece7) ||
            !TryParseInt(tokens[8], out var piece8) ||
            !TryParseInt(tokens[9], out var featureMask))
        {
            entry = default;

            return false;
        }

        entry = new(
            category,
            piece1,
            piece2,
            piece3,
            piece4,
            piece5,
            piece6,
            piece7,
            piece8,
            featureMask,
            tokens[10]
        );

        return true;
    }

    private static bool TryParseInt(string value, out int result)
    {
        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, null, out result);
        }

        return int.TryParse(trimmed, out result);
    }
}
