using System.Text.Json;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts ModernUO's spawners (<c>Distribution/Data/Spawns/&lt;era&gt;/&lt;map&gt;/*.json</c>) of the chosen maps
///     into spawn regions, one small area per spawner, for the maps UOX3 has no spawns for. It reads the <c>shared</c>
///     and <c>post-uoml</c> eras, the world of a modern client, and writes <c>&lt;map&gt;/modernuo_&lt;file&gt;.toml</c>,
///     replacing the <c>modernuo_</c> files of the map it wrote before.
/// </summary>
internal static class ModernUoSpawnConverter
{
    private const string FilePrefix = "modernuo_";

    // How far above its spawner a spot may be, so a spawner in a cave does not spawn on the hill over it.
    private const int Headroom = 16;

    private static readonly string[] Eras = ["shared", "post-uoml"];

    public static int Run(
        string source,
        IReadOnlyList<MapType> maps,
        string mobileDestination,
        string spawnsDestination,
        TextWriter output,
        TextWriter error
    )
    {
        if (!Directory.Exists(source))
        {
            error.WriteLine($"ModernUO spawns folder does not exist: {source}");

            return 2;
        }

        var ids = UoxSpawnConverter.ReadMobileIds(mobileDestination);
        var flatIds = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var id in ids.Order(StringComparer.Ordinal))
        {
            flatIds.TryAdd(ModernUoMobileNames.Flat(id), id);
        }

        var report = new ConversionReport();
        var written = 0;

        foreach (var map in maps)
        {
            written += ConvertMap(source, map, ids, flatIds, spawnsDestination, report, output);
        }

        ConverterOutput.WriteReport(output, report);
        output.WriteLine($"Converted {written} ModernUO spawner(s) into spawn regions.");

        return 0;
    }

    private static int ConvertMap(
        string source,
        MapType map,
        HashSet<string> ids,
        Dictionary<string, string> flatIds,
        string spawnsDestination,
        ConversionReport report,
        TextWriter output
    )
    {
        // The folder name of both ModernUO and Moongate: termur, not ter_mur.
        var mapName = map.ToString().ToLowerInvariant();
        var byFile = new SortedDictionary<string, List<SpawnTemplate>>(StringComparer.Ordinal);

        foreach (var era in Eras)
        {
            var folder = Path.Combine(source, era, mapName);

            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
            {
                var stem = StringUtils.ToSnakeCase(Path.GetFileNameWithoutExtension(file));

                if (!byFile.TryGetValue(stem, out var spawns))
                {
                    byFile[stem] = spawns = [];
                }

                using var document = JsonDocument.Parse(File.ReadAllText(file));

                foreach (var spawner in document.RootElement.EnumerateArray())
                {
                    if (Build(spawner, map, ids, flatIds, report) is { } spawn)
                    {
                        spawn.Id = $"{mapName}_modernuo_{stem}_{spawns.Count}";
                        spawns.Add(spawn);
                    }
                }
            }
        }

        var mapDestination = Path.Combine(spawnsDestination, mapName);

        if (Directory.Exists(mapDestination))
        {
            foreach (var old in Directory.EnumerateFiles(mapDestination, FilePrefix + "*.toml"))
            {
                File.Delete(old);
            }
        }

        var written = 0;

        foreach (var (stem, spawns) in byFile.Where(pair => pair.Value.Count > 0))
        {
            var path = Path.Combine(mapDestination, FilePrefix + stem + ".toml");
            Directory.CreateDirectory(mapDestination);
            TomlUtils.SerializeToFile(new SpawnTemplateFile { Spawn = spawns }, path);
            written += spawns.Count;
            output.WriteLine($"spawns/{mapName}/{Path.GetFileName(path)} ({spawns.Count} spawn region(s))");
        }

        return written;
    }

    private static SpawnTemplate? Build(
        JsonElement spawner,
        MapType map,
        HashSet<string> ids,
        Dictionary<string, string> flatIds,
        ConversionReport report
    )
    {
        var mobiles = new List<string>();

        foreach (var entry in spawner.GetProperty("entries").EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString() ?? string.Empty;

            if (ModernUoMobileNames.Resolve(name, flatIds, ids) is { } id)
            {
                if (!mobiles.Contains(id))
                {
                    mobiles.Add(id);
                }
            }
            else
            {
                report.Count($"unknown mobile {name}");
            }
        }

        if (mobiles.Count == 0)
        {
            report.Count("spawner without known mobiles skipped");

            return null;
        }

        var location = spawner.GetProperty("location");
        var (x, y, z) = (location[0].GetInt32(), location[1].GetInt32(), location[2].GetInt32());
        var range = Math.Max(1, spawner.TryGetProperty("homeRange", out var home) ? home.GetInt32() : 0);
        var min = Minutes(spawner, "minDelay");
        var max = Math.Max(min, Minutes(spawner, "maxDelay"));

        return new()
        {
            Map = map,
            Name = $"{map} {string.Join(", ", mobiles)}",
            MobileIds = mobiles,
            Max = Math.Max(1, spawner.TryGetProperty("count", out var count) ? count.GetInt32() : 1),
            MinMinutes = min,
            MaxMinutes = max,
            Z = z + Headroom,
            Areas = [new() { X1 = Math.Max(0, x - range), Y1 = Math.Max(0, y - range), X2 = x + range, Y2 = y + range }]
        };
    }

    // A ModernUO delay, "hh:mm:ss", in whole minutes, a minute at least.
    private static int Minutes(JsonElement spawner, string property)
    {
        return spawner.TryGetProperty(property, out var value) && TimeSpan.TryParse(value.GetString(), out var delay)
            ? Math.Max(1, (int)delay.TotalMinutes)
            : 1;
    }
}
