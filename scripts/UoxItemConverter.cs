#:project ../src/Moongate.Server.Ultima/Moongate.Server.Ultima.csproj

// Converts UOX3 item and loot definitions (github.com/UOX3DevTeam/UOX3, data/dfndata/items/**/*.dfn)
// into Moongate ItemTemplate/LootTemplate TOML files. UOX3, not POL: UOX3's get= chains one item off
// another (get=base_item, get=0x1440), which maps directly onto ItemTemplate.BaseId; POL has no
// equivalent, so a POL source would leave BaseId empty for everything.
//
// Usage:
//   dotnet run --file scripts/UoxItemConverter.cs -- --source <file-or-directory> --destination <dir> [--loot-destination <dir>]
//
// --source is a single .dfn file or a directory scanned recursively for *.dfn files. Every block is
// read from every source file, and every block's own Id computed, before any cross-reference (get=,
// a loot entry) is resolved, since a target can live in a different file than the block that names
// it (base_item and base_cutlass do, in real UOX3 data; so do items and the loot tables that drop
// them: lootlists.dfn sits alongside them, referencing headers defined all over the items tree).
// One <name>.toml, holding one [[item]] per convertible item block, is written per source .dfn at
// its own relative path under --destination; --loot-destination, when given, gets the same
// treatment for a [LOOTLIST ...] block's [[loot]] instead, its own tree mirroring templates/loots/
// next to templates/items/ rather than one file mixing both kinds. Without --loot-destination, a
// LOOTLIST block converts nothing, the same as it did before this converter knew about loot at all.
//
// Items - what converts, and what does not:
//   the block's own id=        -> ItemId (a Serial)
//   the block header, or name= when the header is a bare hex -> Id
//   name=                      -> Name, carried verbatim; UOX3 does not separate an internal
//                                 identifier from display text the way POL's Name/desc pair does,
//                                 so this often wants a human pass afterwards
//   get=<one target>           -> BaseId, only when that target itself became a converted item and
//                                 only when get= names exactly one target; get=a b (an alias block)
//                                 has no id= of its own and is not converted at all
//   movable=1                  -> Movable = true; anything else (0, 2, or absent) -> false
//   color=                     -> Hue, as a fixed RangeValueSpec<int>
//   weightmax=                 -> MaxWeight
// Weight, value, layer, good, flag, type, combat stats, colorlist, pileable (tiledata already has
// it, see docs/templates.md), amount, dyeable, decay, the multi geometry fields, script,
// spawnobj(list), the archery fields and origin have no home in ItemTemplate yet and are dropped.
// script= is dropped rather than copied into ScriptId: it names a UOX3 JS script, not a Moongate Lua
// module, and copying it across would look like a working reference when it is not one.
//
// Loot - a [LOOTLIST name] block is a weighted table, one bare line per entry (verified against the
// real engine, source/items.cpp's CItem::CreateRandomItem, not just the .dfn shape):
//   weight|entry[,amount]      entry is an item header, LOOTLIST=other (a nested, weighted pick from
//                               another table), or the literal blank (a real, weighted chance of
//                               dropping nothing). weight defaults to 1 when the "weight|" prefix is
//                               absent; amount is a single count or "min max" (space, not a dash).
// Each resolvable entry becomes a LootEntry: an item header resolves through the same Id map get=
// uses, LOOTLIST=other becomes LootEntry.LootTemplateId once "other" is confirmed to be a real
// table, and blank becomes an entry with neither ItemId nor LootTemplateId set. ITEMLIST=, a
// different "spawn everything" mechanic UOX3 also allows in this slot, never appears in real
// lootlists.dfn data and has no home here; an entry this converter cannot resolve any other way is
// dropped, same as an unresolved get=.

using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Types.Templates;

return UoxItemConverter.Run(args);

internal static class UoxItemConverter
{
    public static int Run(string[] args)
    {
        string? source = null;
        string? destination = null;
        string? lootDestination = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--source" when i + 1 < args.Length:
                    source = args[++i];
                    break;
                case "--destination" when i + 1 < args.Length:
                    destination = args[++i];
                    break;
                case "--loot-destination" when i + 1 < args.Length:
                    lootDestination = args[++i];
                    break;
                case "-h" or "--help":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        if (source is null || destination is null)
        {
            PrintUsage();
            return 2;
        }

        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        lootDestination = lootDestination is null ? null : Path.GetFullPath(lootDestination);

        if (!File.Exists(source) && !Directory.Exists(source))
        {
            Console.Error.WriteLine($"Source does not exist: {source}");
            return 2;
        }

        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());

        var sourceFiles = File.Exists(source)
                              ? [source]
                              : Directory.EnumerateFiles(source, "*.dfn", SearchOption.AllDirectories).ToArray();

        if (sourceFiles.Length == 0)
        {
            Console.Error.WriteLine($"No .dfn files found under {source}");
            return 2;
        }

        var blocksByFile = new Dictionary<string, List<DfnBlock>>(StringComparer.Ordinal);
        var blocksByHeader = new Dictionary<string, DfnBlock>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sourceFiles)
        {
            var blocks = DfnParser.Parse(File.ReadAllLines(file));
            blocksByFile[file] = blocks;

            foreach (var block in blocks)
            {
                // A later duplicate header is a malformed source; keep the first one and say so,
                // rather than silently letting the second overwrite what the first already resolved to.
                if (!blocksByHeader.TryAdd(block.Header, block))
                {
                    Console.Error.WriteLine($"Duplicate block '[{block.Header}]' in {file}; keeping the first one seen.");
                }
            }
        }

        // Every block's own Id is computed once, up front, from the block alone - never from another
        // block's Id - so a get= chain or a loot entry resolves the same way no matter which order
        // the source files happen to scan in.
        var convertLoot = lootDestination is not null;
        var idByHeader = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lootIdByHeader = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var knownLootIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in blocksByHeader.Values)
        {
            if (convertLoot && LootTemplateBuilder.TryGetLootId(block.Header, out var lootId))
            {
                lootIdByHeader[block.Header] = lootId;
                knownLootIds.Add(lootId);
            }
            else if (ItemTemplateBuilder.TryComputeId(block, out var id, out _))
            {
                idByHeader[block.Header] = id;
            }
        }

        var written = 0;
        var lootWritten = 0;
        var skippedDuplicate = 0;
        var skippedNoId = 0;
        var skippedUnresolvedLootEntry = 0;

        foreach (var (file, blocks) in blocksByFile)
        {
            var templates = new List<ItemTemplate>();
            var lootTemplates = new List<LootTemplate>();

            foreach (var block in blocks)
            {
                // A header seen again later in the scan lost the "Duplicate block" warning above;
                // it must also lose the conversion, or the same Id comes out of two different
                // files (real UOX3 data does this: food/rawfoods.dfn and misc/rawfoods.dfn both
                // define [0x1e15]).
                if (!ReferenceEquals(blocksByHeader[block.Header], block))
                {
                    skippedDuplicate++;

                    continue;
                }

                if (convertLoot && lootIdByHeader.TryGetValue(block.Header, out var lootId))
                {
                    lootTemplates.Add(LootTemplateBuilder.Build(block, lootId, idByHeader, knownLootIds, out var skipped));
                    skippedUnresolvedLootEntry += skipped;

                    continue;
                }

                var template = ItemTemplateBuilder.Build(block, idByHeader);

                if (template is null)
                {
                    skippedNoId++;

                    continue;
                }

                templates.Add(template);
            }

            var relative = Path.GetRelativePath(Directory.Exists(source) ? source : Path.GetDirectoryName(source)!, file);

            if (templates.Count > 0)
            {
                var outputPath = Path.Combine(destination, Path.ChangeExtension(relative, ".toml"));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                TomlUtils.SerializeToFile(new ItemTemplateFile { Item = templates }, outputPath);
                written += templates.Count;

                Console.WriteLine($"{relative} -> {Path.GetRelativePath(destination, outputPath)} ({templates.Count} item(s))");
            }

            if (lootTemplates.Count > 0)
            {
                var lootOutputPath = Path.Combine(lootDestination!, Path.ChangeExtension(relative, ".toml"));
                Directory.CreateDirectory(Path.GetDirectoryName(lootOutputPath)!);
                TomlUtils.SerializeToFile(new LootTemplateFile { Loot = lootTemplates }, lootOutputPath);
                lootWritten += lootTemplates.Count;

                Console.WriteLine($"{relative} -> {Path.GetRelativePath(lootDestination!, lootOutputPath)} ({lootTemplates.Count} loot table(s))");
            }
        }

        Console.WriteLine(
            $"Converted {written} item(s) and {lootWritten} loot table(s); skipped {skippedNoId} block(s) with no " +
            $"id= of their own, {skippedDuplicate} duplicate of an already-converted header, and " +
            $"{skippedUnresolvedLootEntry} loot entry/entries pointing at nothing this converter could resolve."
        );

        return 0;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            """
            Usage: dotnet run --file scripts/UoxItemConverter.cs -- --source <file-or-directory> --destination <dir> [--loot-destination <dir>]

              --source           A single .dfn file, or a directory scanned recursively for *.dfn files.
              --destination      Directory to write the converted ItemTemplate .toml files under.
              --loot-destination Directory to write the converted LootTemplate .toml files under.
                                 Omit it to leave every [LOOTLIST ...] block unconverted.
            """
        );
    }
}

/// <summary>One <c>[header] { ... }</c> block read from a UOX3 <c>.dfn</c> file: <see cref="Fields" />
/// for an item block's flat <c>key=value</c> lines, <see cref="Entries" /> for every line verbatim,
/// which is what a <c>[LOOTLIST ...]</c> block's bare, unkeyed entry lines need instead.</summary>
internal sealed record DfnBlock(string Header, Dictionary<string, string> Fields, List<string> Entries);

/// <summary>Reads UOX3's <c>.dfn</c> block format: <c>// comment</c> lines, blank lines, a
/// <c>[header]</c> line, a bare <c>{</c>, one line per entry, and a bare <c>}</c>. A trailing
/// <c>//comment</c> is stripped from every line first, real data has it glued straight onto a brace
/// with no space (<c>{//approximately 1%</c>), which otherwise hides the whole block: the real
/// engine (<c>oldstrutil::removeTrailing(sLine, "//")</c> in UOX3's own <c>ssection.cpp</c>) does the
/// same, unconditionally, before looking at a line's content.</summary>
internal static class DfnParser
{
    public static List<DfnBlock> Parse(IReadOnlyList<string> lines)
    {
        var blocks = new List<DfnBlock>();
        string? header = null;
        Dictionary<string, string>? fields = null;
        List<string>? entries = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var commentIndex = line.IndexOf("//", StringComparison.Ordinal);

            if (commentIndex >= 0)
            {
                line = line[..commentIndex].TrimEnd();
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                header = line[1..^1];
                fields = null;
                entries = null;

                continue;
            }

            if (line == "{")
            {
                fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                entries = [];

                continue;
            }

            if (line == "}")
            {
                if (header is not null && fields is not null && entries is not null)
                {
                    blocks.Add(new DfnBlock(header, fields, entries));
                }

                header = null;
                fields = null;
                entries = null;

                continue;
            }

            if (fields is null || entries is null)
            {
                continue;
            }

            entries.Add(line);

            var separator = line.IndexOf('=');

            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            fields[key] = value;
        }

        return blocks;
    }
}

/// <summary>Builds an <see cref="ItemTemplate" /> from one parsed block, resolving its <c>get=</c>
/// target against a fully precomputed header-to-Id map.</summary>
internal static class ItemTemplateBuilder
{
    /// <summary>
    /// Computes a block's Id and item Serial, with no dependency on any other block. Used both to
    /// precompute the full header-to-Id map up front and, once that map exists, by <see cref="Build" />.
    /// False for a block with no id= of its own (a get=a b alias, or a non-item block).
    /// </summary>
    public static bool TryComputeId(DfnBlock block, out string id, out Serial itemId)
    {
        if (!block.Fields.TryGetValue("id", out var idText) || !Serial.TryParse(idText, out itemId))
        {
            id = "";
            itemId = default;

            return false;
        }

        // The header alone is always unique (duplicates are caught and warned about while every
        // block is being read). name= is not: UOX3 reuses it across many facing, material or
        // damage-state variants of the same conceptual thing, sometimes literally "#", so it can
        // only ever be an addition to the header, never a replacement for it.
        id = IsBareHex(block.Header) && block.Fields.TryGetValue("name", out var name) && name.Length > 0
                 ? $"{block.Header}_{name}"
                 : block.Header;

        return true;
    }

    public static ItemTemplate? Build(DfnBlock block, IReadOnlyDictionary<string, string> idByHeader)
    {
        if (!TryComputeId(block, out var id, out var itemId))
        {
            return null;
        }

        var template = new ItemTemplate
        {
            Id = id,
            ItemId = itemId,
            Movable = block.Fields.TryGetValue("movable", out var movable) && movable == "1"
        };

        if (block.Fields.TryGetValue("name", out var displayName) && displayName.Length > 0)
        {
            template.Name = displayName;
        }

        if (block.Fields.TryGetValue("color", out var colorText) && Serial.TryParse(colorText, out var color))
        {
            template.Hue = RangeValueSpec<int>.FromValue((int)color.Value);
        }

        if (block.Fields.TryGetValue("weightmax", out var weightMaxText) && int.TryParse(weightMaxText, out var weightMax))
        {
            template.MaxWeight = weightMax;
        }

        if (block.Fields.TryGetValue("get", out var getText))
        {
            var targets = getText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Only single-parent inheritance maps onto BaseId. get=a b names an alias, not a parent;
            // an unresolved single target (its own block had no id=, or was never converted) is
            // dropped the same as any other field this converter cannot carry over faithfully.
            if (targets.Length == 1 && idByHeader.TryGetValue(targets[0], out var baseId))
            {
                template.BaseId = baseId;
            }
        }

        return template;
    }

    private static bool IsBareHex(string header)
        => header.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Builds a <see cref="LootTemplate" /> from one <c>[LOOTLIST name]</c> block, resolving each
/// entry's item or nested-table reference against the same maps <see cref="ItemTemplateBuilder" /> uses.</summary>
internal static class LootTemplateBuilder
{
    private const string HeaderPrefix = "LOOTLIST ";
    private const string NestedLootPrefix = "LOOTLIST=";
    private const string NestedItemListPrefix = "ITEMLIST=";

    /// <summary>True when <paramref name="header" /> names a loot block (<c>"LOOTLIST name"</c>),
    /// with the table's own Id, everything after the prefix, as <paramref name="lootId" />.</summary>
    public static bool TryGetLootId(string header, out string lootId)
    {
        if (header.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            lootId = header[HeaderPrefix.Length..].Trim();

            return true;
        }

        lootId = "";

        return false;
    }

    public static LootTemplate Build(
        DfnBlock block,
        string id,
        IReadOnlyDictionary<string, string> idByHeader,
        IReadOnlySet<string> knownLootIds,
        out int skippedEntries
    )
    {
        var entries = new List<LootEntry>();
        skippedEntries = 0;

        foreach (var rawLine in block.Entries)
        {
            var entry = ParseEntry(rawLine, idByHeader, knownLootIds);

            if (entry is null)
            {
                skippedEntries++;

                continue;
            }

            entries.Add(entry);
        }

        return new LootTemplate { Id = id, Entries = entries };
    }

    private static LootEntry? ParseEntry(
        string rawLine,
        IReadOnlyDictionary<string, string> idByHeader,
        IReadOnlySet<string> knownLootIds
    )
    {
        var weight = 1;
        var rest = rawLine;
        var pipe = rawLine.IndexOf('|');

        if (pipe >= 0)
        {
            if (!int.TryParse(rawLine[..pipe].Trim(), out weight))
            {
                weight = 1;
            }

            rest = rawLine[(pipe + 1)..].Trim();
        }

        var reference = rest;
        string? amountText = null;
        var comma = rest.IndexOf(',');

        if (comma >= 0)
        {
            reference = rest[..comma].Trim();
            amountText = rest[(comma + 1)..].Trim();
        }

        var amount = ParseAmount(amountText);

        if (reference.Equals("blank", StringComparison.OrdinalIgnoreCase))
        {
            return new LootEntry { Weight = weight, Amount = amount };
        }

        if (reference.StartsWith(NestedLootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var nestedId = reference[NestedLootPrefix.Length..].Trim();

            return knownLootIds.Contains(nestedId)
                       ? new LootEntry { Weight = weight, LootTemplateId = nestedId, Amount = amount }
                       : null;
        }

        // ITEMLIST=, UOX3's "spawn everything in this list" sibling to LOOTLIST=, has no home in
        // LootEntry: it is a different mechanic (spawn every entry, not pick one), and never appears
        // in real lootlists.dfn data.
        if (reference.StartsWith(NestedItemListPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return idByHeader.TryGetValue(reference, out var itemId)
                   ? new LootEntry { Weight = weight, ItemId = itemId, Amount = amount }
                   : null;
    }

    private static RangeValueSpec<int> ParseAmount(string? amountText)
    {
        if (string.IsNullOrEmpty(amountText))
        {
            return RangeValueSpec<int>.FromValue(1);
        }

        var parts = amountText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2 && int.TryParse(parts[0], out var min) && int.TryParse(parts[1], out var max))
        {
            return RangeValueSpec<int>.FromRange(min, max);
        }

        return int.TryParse(parts[0], out var value) ? RangeValueSpec<int>.FromValue(value) : RangeValueSpec<int>.FromValue(1);
    }
}

/// <summary>The root of one converted item TOML file: an array of tables under <c>item</c>.</summary>
internal sealed class ItemTemplateFile
{
    public List<ItemTemplate> Item { get; set; } = [];
}

/// <summary>The root of one converted loot TOML file: an array of tables under <c>loot</c>.</summary>
internal sealed class LootTemplateFile
{
    public List<LootTemplate> Loot { get; set; } = [];
}
