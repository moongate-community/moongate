using System.Globalization;
using System.Text.Json;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts the treasure chests of ModernUO's spawners (
///     <c>Distribution/Data/Spawns/&lt;era&gt;/&lt;map&gt;/*.json</c>,
///     the entries named <c>TreasureChestLevel1</c> to <c>4</c>) into spawn regions of items, one per spawner
///     with chests, and
///     writes them as <c>&lt;map&gt;/treasure_chests.toml</c>, replacing that of a previous run. The creatures of the
///     same spawners are left to the spawn converters.
/// </summary>
internal static class ModernUoChestConverter
{
    private const string FileName = "treasure_chests.toml";
    private const string ClassPrefix = "TreasureChestLevel";
    private const int Levels = 4;

    public static int Run(string source, string destination, TextWriter output, TextWriter error)
    {
        if (!Directory.Exists(source))
        {
            error.WriteLine($"ModernUO spawns folder does not exist: {source}");

            return 2;
        }

        var report = new ConversionReport();
        var byMap = new SortedDictionary<string, List<SpawnTemplate>>(StringComparer.Ordinal);

        foreach (var map in Enum.GetValues<MapType>())
        {
            // The folder name of both ModernUO and Moongate: termur, not ter_mur.
            var mapName = map.ToString().ToLowerInvariant();

            foreach (var era in ModernUoSpawnConverter.Eras)
            {
                var folder = Path.Combine(source, era, mapName);

                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
                {
                    var stem = StringUtils.ToSnakeCase(Path.GetFileNameWithoutExtension(file));
                    using var document = JsonDocument.Parse(File.ReadAllText(file));
                    var index = 0;

                    foreach (var spawner in document.RootElement.EnumerateArray())
                    {
                        var id = $"{mapName}_chest_{StringUtils.ToSnakeCase(era)}_{stem}_{index++}";
                        var chests = Build(spawner, id, map, report).ToList();

                        if (chests.Count > 0)
                        {
                            if (!byMap.TryGetValue(mapName, out var spawns))
                            {
                                byMap[mapName] = spawns = [];
                            }

                            spawns.AddRange(chests);
                        }
                    }
                }
            }
        }

        if (byMap.Count == 0)
        {
            error.WriteLine($"{source}: no treasure chest in its spawners; it must hold folders such as shared/felucca.");

            return 2;
        }

        foreach (var (mapName, spawns) in byMap)
        {
            var path = Path.Combine(destination, mapName, FileName);
            // Safe: GetDirectoryName is null only for a root or empty path, not a file combined under a folder.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            TomlUtils.SerializeToFile(new SpawnTemplateFile { Spawn = spawns }, path);
            output.WriteLine($"spawns/{mapName}/{FileName} ({spawns.Count} chest region(s))");
        }

        ConverterOutput.WriteReport(output, report);
        output.WriteLine($"Wrote {byMap.Values.Sum(spawns => spawns.Count)} chest region(s) from ModernUO's spawners.");

        return 0;
    }

    // One region for the chests of a spawner: it picks among their levels, and as many live at once as their caps
    // allow, the spawner's count at most, which they share.
    private static IEnumerable<SpawnTemplate> Build(JsonElement spawner, string id, MapType map, ConversionReport report)
    {
        var count = Math.Max(1, spawner.TryGetProperty("count", out var countValue) ? countValue.GetInt32() : 1);
        var levels = new SortedSet<int>();
        var caps = 0;

        foreach (var entry in spawner.GetProperty("entries").EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString() ?? string.Empty;

            if (!name.StartsWith(ClassPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(name[ClassPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var level) ||
                level is < 1 or > Levels)
            {
                report.Count($"unknown chest {name}");

                continue;
            }

            levels.Add(level);
            caps += Math.Clamp(entry.TryGetProperty("maxCount", out var maxCount) ? maxCount.GetInt32() : count, 1, count);
        }

        if (levels.Count == 0)
        {
            yield break;
        }

        var min = ModernUoSpawnConverter.Minutes(spawner, "minDelay");
        var spawn = new SpawnTemplate
        {
            Id = id,
            Map = ModernUoSpawnConverter.MapOf(spawner, map),
            Name = $"Treasure chest level {string.Join(", ", levels)}",
            ItemIds = levels.Select(level => $"treasure_chest_level_{level}").ToList(),
            Max = Math.Min(count, caps),
            MinMinutes = min,
            MaxMinutes = Math.Max(min, ModernUoSpawnConverter.Minutes(spawner, "maxDelay"))
        };
        ModernUoSpawnConverter.Place(spawner, spawn);

        yield return spawn;
    }
}
