using System.Text.RegularExpressions;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts UOX3's NPC lists (<c>[NPCLIST name]</c> under <c>npc/</c>) and spawn regions (<c>[REGIONSPAWN n]</c>
///     under <c>spawn/</c>) of a dfndata folder, after the mobile pass, against the mobile templates it wrote.
/// </summary>
internal static partial class UoxSpawnConverter
{
    private const string ListHeaderPrefix = "NPCLIST ";
    private const string NestedListPrefix = "NPCLIST=";
    private const string SpawnHeaderPrefix = "REGIONSPAWN ";

    // The era of the shard: the client is a modern one. UOX3's own default, lbr, keeps the same regions.
    private const string ShardEra = "tol";

    // What a region never takes from the region it GETs, as UOX3 loads a parent without them.
    private static readonly HashSet<string> NotInherited = new(StringComparer.Ordinal) { "WORLD", "NPC", "NPCLIST", "ERAS" };

    // UOX3's WORLD numbers.
    private static readonly Dictionary<int, MapType> Worlds = new()
    {
        [0] = MapType.Felucca, [1] = MapType.Trammel, [2] = MapType.Ilshenar, [3] = MapType.Malas, [4] = MapType.Tokuno
    };

    public static int Run(
        string mobileSource,
        string mobileDestination,
        string npcListsDestination,
        string spawnsDestination,
        TextWriter output,
        TextWriter error
    )
    {
        var report = new ConversionReport();
        var mobileIds = ReadMobileIds(mobileDestination);
        var lists = ConvertLists(mobileSource, npcListsDestination, mobileIds, report, output);
        var spawns = ConvertSpawns(mobileSource, spawnsDestination, mobileIds, lists, report, output);

        ConverterOutput.WriteReport(output, report);
        output.WriteLine($"Converted {lists.Count} npc list(s) and {spawns} spawn region(s).");

        var errors = Verify(npcListsDestination, spawnsDestination, mobileIds, out var verifiedLists, out var verifiedSpawns);

        if (ConverterOutput.ReportErrors(error, errors, "spawns") != 0)
        {
            return 1;
        }

        output.WriteLine(
            $"Verified {verifiedLists} npc list(s) and {verifiedSpawns} spawn region(s) read back from disk: every mobile and " +
            "list resolves, every spawn has an area and its times in order."
        );

        return 0;
    }

    internal static HashSet<string> ReadMobileIds(string mobileDestination)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        if (!Directory.Exists(mobileDestination))
        {
            return ids;
        }

        foreach (var file in Directory.EnumerateFiles(mobileDestination, "*.toml", SearchOption.AllDirectories))
        {
            foreach (var mobile in TomlUtils.DeserializeFromFile<MobileTemplateFile>(file)?.Mobile ?? [])
            {
                ids.Add(mobile.Id);
            }
        }

        return ids;
    }

    private static HashSet<string> ConvertLists(
        string mobileSource,
        string destination,
        HashSet<string> mobileIds,
        ConversionReport report,
        TextWriter output
    )
    {
        var npcDirectory = Path.Combine(mobileSource, "npc");
        var byFile = new List<(string Relative, List<NpcListTemplate> Lists)>();
        var byId = new Dictionary<string, NpcListTemplate>(StringComparer.Ordinal);
        var raw = new Dictionary<string, List<(NpcListEntry Entry, bool Splice)>>(StringComparer.Ordinal);

        var files = Directory.Exists(npcDirectory)
            ? Directory.EnumerateFiles(npcDirectory, "*.dfn", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList()
            : [];

        foreach (var file in files)
        {
            var lists = new List<NpcListTemplate>();

            foreach (var block in DfnParser.Parse(File.ReadAllLines(file)))
            {
                if (!block.Header.StartsWith(ListHeaderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var list = new NpcListTemplate { Id = StringUtils.ToSnakeCase(block.Header[ListHeaderPrefix.Length..].Trim()) };

                if (!byId.TryAdd(list.Id, list))
                {
                    report.Count("duplicate npc list");

                    continue;
                }

                raw[list.Id] = block.Entries.Select(ParseListEntry).OfType<(NpcListEntry Entry, bool Splice)>().ToList();
                lists.Add(list);
            }

            if (lists.Count > 0)
            {
                var root = Path.Combine(npcDirectory, "npclists");
                var relative = file.StartsWith(root, StringComparison.Ordinal) ? Path.GetRelativePath(root, file) : Path.GetRelativePath(npcDirectory, file);
                byFile.Add((relative, lists));
            }
        }

        // An unweighted NPCLIST=x brings x's entries into the list, as UOX3 splices it; a weighted one stays one pick.
        foreach (var list in byId.Values)
        {
            list.Entries = Splice(list.Id, raw, []);
        }

        // Drop what does not resolve, then the lists left empty and the entries naming them, until nothing changes.
        foreach (var list in byId.Values)
        {
            list.Entries.RemoveAll(
                entry =>
                {
                    var unresolved = entry.MobileId is { } mobile ? !mobileIds.Contains(mobile) : !byId.ContainsKey(entry.NpcListId!);

                    if (unresolved)
                    {
                        report.Count("unresolved npc list entry");
                    }

                    return unresolved;
                }
            );
        }

        bool changed;

        do
        {
            changed = false;

            foreach (var empty in byId.Values.Where(list => list.Entries.Count == 0).ToList())
            {
                byId.Remove(empty.Id);
                report.Count("empty npc list");
                changed = true;
            }

            foreach (var list in byId.Values)
            {
                changed |= list.Entries.RemoveAll(entry => entry.NpcListId is { } nested && !byId.ContainsKey(nested)) > 0;
            }
        }
        while (changed);

        foreach (var (relative, lists) in byFile)
        {
            var kept = lists.Where(list => byId.ContainsKey(list.Id)).ToList();

            if (kept.Count == 0)
            {
                continue;
            }

            var outputPath = Path.Combine(destination, Path.ChangeExtension(relative, ".toml"));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            TomlUtils.SerializeToFile(new NpcListTemplateFile { NpcList = kept }, outputPath);
            output.WriteLine($"npc/{relative} -> {Path.GetRelativePath(destination, outputPath)} ({kept.Count} npc list(s))");
        }

        return byId.Keys.ToHashSet(StringComparer.Ordinal);
    }

    private static List<NpcListEntry> Splice(
        string id,
        Dictionary<string, List<(NpcListEntry Entry, bool Splice)>> raw,
        HashSet<string> visiting
    )
    {
        var entries = new List<NpcListEntry>();

        if (!raw.TryGetValue(id, out var own) || !visiting.Add(id))
        {
            return entries;
        }

        foreach (var (entry, splice) in own)
        {
            if (splice)
            {
                // A spliced list that does not exist is kept as a reference, so it is reported as unresolved.
                entries.AddRange(raw.ContainsKey(entry.NpcListId!) ? Splice(entry.NpcListId!, raw, visiting) : [entry]);
            }
            else
            {
                entries.Add(entry);
            }
        }

        visiting.Remove(id);

        return entries;
    }

    // "20|gorilla", "orc", "NPCLIST=trolls" (spliced) or "7|NPCLIST=allophidians" (one pick).
    private static (NpcListEntry Entry, bool Splice)? ParseListEntry(string rawLine)
    {
        var line = rawLine.Trim();
        var weight = 1;
        var pipe = line.IndexOf('|');

        if (pipe >= 0)
        {
            weight = int.TryParse(line[..pipe].Trim(), out var parsed) && parsed > 0 ? parsed : 1;
            line = line[(pipe + 1)..].Trim();
        }

        if (line.Length == 0)
        {
            return null;
        }

        return line.StartsWith(NestedListPrefix, StringComparison.OrdinalIgnoreCase)
            ? (new() { Weight = weight, NpcListId = StringUtils.ToSnakeCase(line[NestedListPrefix.Length..].Trim()) }, pipe < 0)
            : (new NpcListEntry { Weight = weight, MobileId = StringUtils.ToSnakeCase(line) }, false);
    }

    private static int ConvertSpawns(
        string mobileSource,
        string destination,
        HashSet<string> mobileIds,
        HashSet<string> listIds,
        ConversionReport report,
        TextWriter output
    )
    {
        var spawnDirectory = Path.Combine(mobileSource, "spawn");
        var files = Directory.Exists(spawnDirectory)
            ? Directory.EnumerateFiles(spawnDirectory, "*.dfn", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList()
            : [];
        var blocksByFile = files.Select(file => (File: file, Blocks: DfnParser.Parse(File.ReadAllLines(file)))).ToList();
        var byNumber = new Dictionary<string, DfnBlock>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, blocks) in blocksByFile)
        {
            // UOX3 keeps the last definition of a header.
            foreach (var block in blocks.Where(block => block.Header.StartsWith(SpawnHeaderPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                byNumber[block.Header[SpawnHeaderPrefix.Length..].Trim()] = block;
            }
        }

        // Collected first: files of two folders can hold regions of the same map under the same name.
        var byOutput = new SortedDictionary<(MapType Map, string Name), List<SpawnTemplate>>();

        foreach (var (file, blocks) in blocksByFile)
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(file)!);
            var stem = Path.GetFileNameWithoutExtension(file);
            var name = StringUtils.ToSnakeCase(SpawnFilePrefix().Replace(stem, string.Empty));

            foreach (var block in blocks.Where(block => block.Header.StartsWith(SpawnHeaderPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                var number = block.Header[SpawnHeaderPrefix.Length..].Trim();

                if (!ReferenceEquals(byNumber[number], block))
                {
                    report.Count("duplicate spawn region");

                    continue;
                }

                var fields = Resolve(block, byNumber, []);

                if (Build(number, fields, folder, mobileIds, listIds, report) is not { } spawn)
                {
                    continue;
                }

                if (!byOutput.TryGetValue((spawn.Map, name), out var list))
                {
                    byOutput[(spawn.Map, name)] = list = [];
                }

                list.Add(spawn);
            }
        }

        var written = 0;

        foreach (var ((map, name), spawns) in byOutput)
        {
            var outputPath = Path.Combine(destination, EnumNameUtils.Format(map), name + ".toml");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            TomlUtils.SerializeToFile(new SpawnTemplateFile { Spawn = spawns }, outputPath);
            written += spawns.Count;
            output.WriteLine($"spawns/{Path.GetRelativePath(destination, outputPath)} ({spawns.Count} spawn region(s))");
        }

        return written;
    }

    // The KEY=VALUE lines of the region, after those of the region it GETs; its own keys win, and naming an NPC or a
    // list replaces what it would inherit to spawn.
    private static List<(string Key, string Value)> Resolve(DfnBlock block, Dictionary<string, DfnBlock> byNumber, HashSet<string> seen)
    {
        var own = block.Entries.Select(SplitField).OfType<(string Key, string Value)>().ToList();
        var parent = own.FirstOrDefault(field => field.Key == "GET").Value;

        if (parent is null || !seen.Add(parent) || !byNumber.TryGetValue(parent, out var parentBlock))
        {
            return own.Where(field => field.Key != "GET").ToList();
        }

        var inherited = Resolve(parentBlock, byNumber, seen);
        var ownKeys = own.Select(field => field.Key).ToHashSet();

        return inherited.Where(field => !ownKeys.Contains(field.Key) && !NotInherited.Contains(field.Key))
                        .Concat(own.Where(field => field.Key != "GET"))
                        .ToList();
    }

    private static (string Key, string Value)? SplitField(string line)
    {
        var equals = line.IndexOf('=');

        return equals > 0 ? (line[..equals].Trim().ToUpperInvariant(), line[(equals + 1)..].Trim()) : null;
    }

    private static SpawnTemplate? Build(
        string number,
        List<(string Key, string Value)> fields,
        string folder,
        HashSet<string> mobileIds,
        HashSet<string> listIds,
        ConversionReport report
    )
    {
        string? Get(string key) => fields.LastOrDefault(field => field.Key == key).Value;
        int? Number(params string[] keys) => keys.Select(Get).OfType<string>().Select(text => UoxNumber.TryParse(text, out var value) ? (int?)value : null).FirstOrDefault(value => value is not null);

        var mobiles = fields.Where(field => field.Key == "NPC").Select(field => StringUtils.ToSnakeCase(field.Value)).ToList();
        var lists = fields.Where(field => field.Key == "NPCLIST").Select(field => StringUtils.ToSnakeCase(field.Value)).ToList();

        if (mobiles.Count == 0 && lists.Count == 0)
        {
            report.Count("spawn region(s) without NPCs skipped");

            return null;
        }

        if (Get("ERAS") is { } eras &&
            !eras.Split(',').Any(era => era.Trim().Equals(ShardEra, StringComparison.OrdinalIgnoreCase)))
        {
            report.Count("spawn region of another era skipped");

            return null;
        }

        mobiles.RemoveAll(id => !mobileIds.Contains(id));
        lists.RemoveAll(id => !listIds.Contains(id));

        if (mobiles.Count == 0 && lists.Count == 0)
        {
            report.Count("unresolved spawn");

            return null;
        }

        var map = Number("WORLD") is { } world && Worlds.TryGetValue(world, out var worldMap) ? worldMap : MapOfFolder(folder);
        var spawn = new SpawnTemplate
        {
            Id = $"{EnumNameUtils.Format(map)}_{number}",
            Map = map,
            Name = Get("NAME"),
            MobileIds = mobiles.Distinct().ToList(),
            NpcListIds = lists.Distinct().ToList(),
            Max = Number("MAXNPCS", "MAXNPC") ?? 0,
            // UOX3 picks between the two whatever their order.
            MinMinutes = Math.Min(Number("MINTIME") ?? 0, Number("MAXTIME") ?? 0),
            MaxMinutes = Math.Max(Number("MINTIME") ?? 0, Number("MAXTIME") ?? 0),
            Call = Number("CALL") ?? 1,
            PrefZ = Number("PREFZ"),
            Z = Number("DEFZ"),
            OnlyOutside = Number("ONLYOUTSIDE") == 1
        };

        if (Number("X1") is { } x1 && Number("Y1") is { } y1 && Number("X2") is { } x2 && Number("Y2") is { } y2)
        {
            spawn.Areas.Add(new() { X1 = Math.Min(x1, x2), Y1 = Math.Min(y1, y2), X2 = Math.Max(x1, x2), Y2 = Math.Max(y1, y2) });
        }

        foreach (var exclude in fields.Where(field => field.Key == "EXCLUDEAREA").Select(field => field.Value.Split(',')))
        {
            if (exclude.Length == 4 && exclude.All(part => UoxNumber.TryParse(part.Trim(), out _)))
            {
                var values = exclude.Select(part => UoxNumber.TryParse(part.Trim(), out var value) ? value : 0).ToArray();
                spawn.Exclude.Add(
                    new()
                    {
                        X1 = Math.Min(values[0], values[2]), Y1 = Math.Min(values[1], values[3]),
                        X2 = Math.Max(values[0], values[2]), Y2 = Math.Max(values[1], values[3])
                    }
                );
            }
        }

        if (spawn.Max < 1 || spawn.Areas.Count == 0)
        {
            report.Count("spawn region(s) without a maximum or an area skipped");

            return null;
        }

        return spawn;
    }

    private static MapType MapOfFolder(string folder)
    {
        return folder.ToLowerInvariant() switch
        {
            "trammel" => MapType.Trammel,
            "ilishenar" or "ilshenar" => MapType.Ilshenar,
            "malas" => MapType.Malas,
            "tokuno" => MapType.Tokuno,
            _ => MapType.Felucca
        };
    }

    // Reads back what was written, as the server will, and checks what its loaders check.
    private static List<string> Verify(
        string npcListsDestination,
        string spawnsDestination,
        HashSet<string> mobileIds,
        out int verifiedLists,
        out int verifiedSpawns
    )
    {
        var errors = new List<string>();
        var lists = Directory.Exists(npcListsDestination)
            ? Directory.EnumerateFiles(npcListsDestination, "*.toml", SearchOption.AllDirectories)
                       .SelectMany(file => TomlUtils.DeserializeFromFile<NpcListTemplateFile>(file)?.NpcList ?? [])
                       .ToList()
            : [];
        var listIds = lists.Select(list => list.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var list in lists)
        {
            foreach (var entry in list.Entries)
            {
                if (entry.MobileId is { } mobile ? !mobileIds.Contains(mobile) : !listIds.Contains(entry.NpcListId ?? string.Empty))
                {
                    errors.Add($"npc list '{list.Id}' names '{entry.MobileId ?? entry.NpcListId}', which does not resolve.");
                }
            }
        }

        var spawns = Directory.Exists(spawnsDestination)
            ? Directory.EnumerateFiles(spawnsDestination, "*.toml", SearchOption.AllDirectories)
                       .SelectMany(file => TomlUtils.DeserializeFromFile<SpawnTemplateFile>(file)?.Spawn ?? [])
                       .ToList()
            : [];

        foreach (var spawn in spawns)
        {
            if (spawn.MobileIds.Any(id => !mobileIds.Contains(id)) || spawn.NpcListIds.Any(id => !listIds.Contains(id)))
            {
                errors.Add($"spawn '{spawn.Id}' names a mobile or a list that does not resolve.");
            }

            if (spawn.Max < 1 || spawn.Call < 1 || spawn.MinMinutes > spawn.MaxMinutes || spawn.Areas.Count == 0 ||
                spawn.Areas.Concat(spawn.Exclude).Any(area => area.X1 > area.X2 || area.Y1 > area.Y2))
            {
                errors.Add($"spawn '{spawn.Id}' has a max, call, times or area the loader refuses.");
            }
        }

        foreach (var id in spawns.GroupBy(spawn => spawn.Id).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            errors.Add($"spawn '{id}' is written twice.");
        }

        verifiedLists = lists.Count;
        verifiedSpawns = spawns.Count;

        return errors;
    }

    [GeneratedRegex("^spawn_[a-z]+_", RegexOptions.IgnoreCase)]
    private static partial Regex SpawnFilePrefix();
}
