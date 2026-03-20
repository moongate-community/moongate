using Moongate.Server.Attributes;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.Files;
using Serilog;
using System.Globalization;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads the Mounts tile list from support/uoconvert.cfg.
/// </summary>
[RegisterFileLoader(24)]
public sealed class MountDataLoader : IFileLoader
{
    private readonly ILogger _logger = Log.ForContext<MountDataLoader>();
    private readonly MountTileData _mountTileData;
    private readonly string _cfgPath;

    public MountDataLoader(MountTileData mountTileData)
        : this(mountTileData, Path.Combine(AppContext.BaseDirectory, "Assets", "data", "support", "uoconvert.cfg"))
    {
    }

    internal MountDataLoader(MountTileData mountTileData, string cfgPath)
    {
        _mountTileData = mountTileData;
        _cfgPath = cfgPath;
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_cfgPath))
        {
            _logger.Warning("Mount data file not found: {FilePath}", _cfgPath);
            _mountTileData.Replace([]);

            return;
        }

        var itemIds = new HashSet<int>();
        var insideMounts = false;

        foreach (var rawLine in await File.ReadAllLinesAsync(_cfgPath))
        {
            var line = StripComment(rawLine).Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (!insideMounts)
            {
                if (line.StartsWith("Mounts", StringComparison.OrdinalIgnoreCase))
                {
                    insideMounts = true;
                }

                continue;
            }

            if (line == "}")
            {
                break;
            }

            if (!line.StartsWith("Tiles", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var token in line["Tiles".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParseItemId(token, out var itemId))
                {
                    itemIds.Add(itemId);
                }
            }
        }

        _mountTileData.Replace(itemIds);
        _logger.Information("Loaded {Count} mount tile ids from {FilePath}", _mountTileData.ItemIds.Count, _cfgPath);
    }

    private static string StripComment(string rawLine)
    {
        var commentIndex = rawLine.IndexOf("//", StringComparison.Ordinal);

        return commentIndex >= 0 ? rawLine[..commentIndex] : rawLine;
    }

    private static bool TryParseItemId(string value, out int itemId)
    {
        itemId = 0;
        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out itemId);
        }

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId);
    }
}
