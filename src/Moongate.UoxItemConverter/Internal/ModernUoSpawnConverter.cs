using System.Globalization;
using System.Text.Json;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts ModernUO's spawners ( <c>Distribution/Data/Spawns/&lt;era&gt;/&lt;map&gt;/*.json</c>) of the chosen
///     maps
///     into spawn regions, for the maps UOX3 has no spawns for. It reads the <c>shared</c> and <c>post-uoml</c> eras,
///     the world of a modern client, and writes <c>&lt;map&gt;/modernuo_&lt;file&gt;.toml</c>, replacing the
///     <c>modernuo_</c> files of the maps it converts. A region keeps the map its spawner names, which may differ
///     from its folder's, and goes into that map's folder, where the server looks for it. A region id names the era, the file
///     and the spawner's index in
///     it, so it stays the same when a later run resolves more mobiles.
/// </summary>
internal static class ModernUoSpawnConverter
{
    private const string FilePrefix = "modernuo_";

    // How far above its spawner a spot may be, so a spawner in a cave does not spawn on the hill over it.
    private const int Headroom = 16;

    internal static readonly string[] Eras = ["shared", "post-uoml"];

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

        // Without templates every spawner would be skipped and the shipped files replaced by nothing.
        if (ids.Count == 0)
        {
            error.WriteLine($"Found no mobile templates under {mobileDestination}.");

            return 2;
        }

        var flatIds = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var id in ids.Order(StringComparer.Ordinal))
        {
            flatIds.TryAdd(ModernUoMobileNames.Flat(id), id);
        }

        var report = new ConversionReport();
        var converted = new List<MapType>();

        // By the map each region names, then by file: the Yomotsu Mines lie in the tokuno folder and on Malas, and the
        // loader takes a region's map from its folder.
        var byMap = new SortedDictionary<MapType, SortedDictionary<string, List<SpawnTemplate>>>();

        foreach (var map in maps)
        {
            var byFile = ConvertMap(source, map, ids, flatIds, report);

            // Nothing to write keeps what a previous run wrote.
            if (byFile.Values.All(spawns => spawns.Count == 0))
            {
                continue;
            }

            converted.Add(map);

            foreach (var (stem, spawns) in byFile)
            {
                foreach (var spawn in spawns)
                {
                    if (!byMap.TryGetValue(spawn.Map, out var files))
                    {
                        byMap[spawn.Map] = files = new(StringComparer.Ordinal);
                    }

                    if (!files.TryGetValue(stem, out var regions))
                    {
                        files[stem] = regions = [];
                    }

                    regions.Add(spawn);
                }
            }
        }

        // Every old file first, then every new one: a map converted later must not delete what an earlier one moved
        // into its folder.
        foreach (var map in converted)
        {
            var mapDestination = Path.Combine(spawnsDestination, FolderOf(map));

            if (Directory.Exists(mapDestination))
            {
                foreach (var old in Directory.EnumerateFiles(mapDestination, FilePrefix + "*.toml"))
                {
                    File.Delete(old);
                }
            }
        }

        var written = 0;

        foreach (var (map, files) in byMap)
        {
            var mapDestination = Path.Combine(spawnsDestination, FolderOf(map));
            Directory.CreateDirectory(mapDestination);

            foreach (var (stem, spawns) in files)
            {
                var path = Path.Combine(mapDestination, FilePrefix + stem + ".toml");
                TomlUtils.SerializeToFile(new SpawnTemplateFile { Spawn = spawns }, path);
                written += spawns.Count;
                output.WriteLine($"spawns/{FolderOf(map)}/{Path.GetFileName(path)} ({spawns.Count} spawn region(s))");
            }
        }

        ConverterOutput.WriteReport(output, report);
        output.WriteLine($"Wrote {written} spawn region(s) from ModernUO's spawners.");

        return 0;
    }

    // The folder name of both ModernUO and Moongate: termur, not ter_mur.
    private static string FolderOf(MapType map)
    {
        return map.ToString().ToLowerInvariant();
    }

    // The regions of a map's ModernUO folders, by file.
    private static SortedDictionary<string, List<SpawnTemplate>> ConvertMap(
        string source,
        MapType map,
        HashSet<string> ids,
        Dictionary<string, string> flatIds,
        ConversionReport report
    )
    {
        var mapName = FolderOf(map);
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
                var index = 0;

                foreach (var spawner in document.RootElement.EnumerateArray())
                {
                    var id = $"{mapName}_modernuo_{StringUtils.ToSnakeCase(era)}_{stem}_{index++}";
                    spawns.AddRange(Build(spawner, id, map, ids, flatIds, report));
                }
            }
        }

        return byFile;
    }

    // ModernUO picks each spawn among the entries under their maxCount, up to the spawner's count. An entry capped
    // below the count becomes a region of its own with its cap; the others share one region with what is left, less
    // the share of the entries no template matches.
    private static IEnumerable<SpawnTemplate> Build(
        JsonElement spawner,
        string id,
        MapType map,
        HashSet<string> ids,
        Dictionary<string, string> flatIds,
        ConversionReport report
    )
    {
        var count = Math.Max(1, spawner.TryGetProperty("count", out var countValue) ? countValue.GetInt32() : 1);
        // By mobile, in entry order: a class listed twice adds its caps.
        var capped = new List<(string Mobile, int Cap)>();
        var shared = new List<string>();
        var unknownShared = 0;
        var cappedTotal = 0;

        foreach (var entry in spawner.GetProperty("entries").EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString() ?? string.Empty;
            var cap = Math.Min(count, entry.TryGetProperty("maxCount", out var maxCount) ? maxCount.GetInt32() : count);
            var mobile = ModernUoMobileNames.Resolve(name, flatIds, ids);

            if (mobile is null)
            {
                report.Count($"unknown mobile {name}");
            }

            if (cap < count)
            {
                cappedTotal += cap;

                if (mobile is not null && cap > 0)
                {
                    var known = capped.FindIndex(pair => pair.Mobile == mobile);

                    if (known < 0)
                    {
                        capped.Add((mobile, cap));
                    }
                    else
                    {
                        capped[known] = (mobile, Math.Min(count, capped[known].Cap + cap));
                    }
                }
            }
            else if (mobile is null)
            {
                unknownShared++;
            }
            else if (!shared.Contains(mobile))
            {
                shared.Add(mobile);
            }
        }

        if (capped.Count == 0 && shared.Count == 0)
        {
            report.Count("spawner without known mobiles skipped");

            yield break;
        }

        var rest = Math.Max(1, count - cappedTotal);

        if (shared.Count > 0)
        {
            var max = Math.Max(
                1,
                (int)Math.Round(rest * shared.Count / (double)(shared.Count + unknownShared), MidpointRounding.AwayFromZero)
            );

            yield return Region(spawner, id, map, shared, max);
        }

        foreach (var (mobile, cap) in capped)
        {
            yield return Region(spawner, $"{id}_{mobile}", map, [mobile], cap);
        }
    }

    private static SpawnTemplate Region(JsonElement spawner, string id, MapType folder, List<string> mobiles, int max)
    {
        var map = MapOf(spawner, folder);
        var min = Minutes(spawner, "minDelay");
        var spawn = new SpawnTemplate
        {
            Id = id,
            Map = map,
            Name = $"{map} {string.Join(", ", mobiles)}",
            MobileIds = [.. mobiles],
            Max = max,
            MinMinutes = min,
            MaxMinutes = Math.Max(min, Minutes(spawner, "maxDelay"))
        };

        Place(spawner, spawn);

        return spawn;
    }

    // The map a spawner names, which is not always its folder's: the Yomotsu Mines and the Fan Dancer's Dojo lie in the
    // tokuno folder and on the Malas map. One that names none takes the folder's.
    internal static MapType MapOf(JsonElement spawner, MapType folder)
    {
        return spawner.TryGetProperty("map", out var value) &&
               value.ValueKind == JsonValueKind.String &&
               Enum.TryParse<MapType>(value.GetString(), true, out var map) &&
               Enum.IsDefined(map)
            ? map
            : folder;
    }

    // The area a spawner spawns in and the height its spots stay under.
    internal static void Place(JsonElement spawner, SpawnTemplate spawn)
    {
        var location = spawner.GetProperty("location");
        var (x, y, z) = (location[0].GetInt32(), location[1].GetInt32(), location[2].GetInt32());

        if (spawner.TryGetProperty("spawnBounds", out var bounds))
        {
            var (start, end) = (bounds.GetProperty("start"), bounds.GetProperty("end"));
            spawn.Areas.Add(
                new()
                {
                    X1 = Math.Max(0, start.GetProperty("x").GetInt32()),
                    Y1 = Math.Max(0, start.GetProperty("y").GetInt32()),
                    X2 = end.GetProperty("x").GetInt32(),
                    Y2 = end.GetProperty("y").GetInt32()
                }
            );
            spawn.Z = end.GetProperty("z").GetInt32();

            return;
        }

        // Without a home range ModernUO spawns on the spawner's own spot.
        var range = Math.Max(0, spawner.TryGetProperty("homeRange", out var home) ? home.GetInt32() : 0);
        spawn.Areas.Add(new() { X1 = Math.Max(0, x - range), Y1 = Math.Max(0, y - range), X2 = x + range, Y2 = y + range });
        spawn.Z = z + Headroom;
    }

    // A ModernUO delay, "hh:mm:ss", in whole minutes, a minute at least.
    internal static int Minutes(JsonElement spawner, string property)
    {
        return spawner.TryGetProperty(property, out var value) &&
               TimeSpan.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var delay)
            ? Math.Max(1, (int)delay.TotalMinutes)
            : 1;
    }
}
