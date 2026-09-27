using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     The converter's real logic, testable in-process: no CLI parsing, no <see cref="Environment.ExitCode" />,
///     output written to the given writers rather than <see cref="Console" /> directly.
///     <c>
///         Program.cs
///     </c>
///     is the only caller that goes through
///     <c>
///         ConsoleApp.Run
///     </c>
///     .
/// </summary>
internal static class UoxItemConverterCommand
{
    public static int Run(
        string source,
        string destination,
        string? lootDestination,
        TextWriter output,
        TextWriter error,
        string? mobileSource = null,
        string? mobileDestination = null,
        string? namesDestination = null,
        string? startingItemsDestination = null
    )
    {
        if ((mobileSource is null) != (mobileDestination is null) || (mobileSource is null) != (namesDestination is null))
        {
            error.WriteLine("--mobile-source, --mobile-destination and --names-destination go together: give all three or none.");

            return 2;
        }

        if (startingItemsDestination is not null && mobileSource is null)
        {
            error.WriteLine("--starting-items-destination needs --mobile-source, which holds newbie/newbie.dfn.");

            return 2;
        }

        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        lootDestination = lootDestination is null ? null : Path.GetFullPath(lootDestination);

        if (!File.Exists(source) && !Directory.Exists(source))
        {
            error.WriteLine($"Source does not exist: {source}");

            return 2;
        }

        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new Point2DTomlConverter());
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());

        var sourceFiles = File.Exists(source)
            ? [source]
            : Directory.EnumerateFiles(source, "*.dfn", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();

        if (sourceFiles.Length == 0)
        {
            error.WriteLine($"No .dfn files found under {source}");

            return 2;
        }

        var blocksByFile = new Dictionary<string, List<DfnBlock>>(StringComparer.Ordinal);
        var blocksByHeader = new Dictionary<string, DfnBlock>(StringComparer.OrdinalIgnoreCase);
        var flatByHeader = new Dictionary<string, DfnBlock>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sourceFiles)
        {
            var blocks = DfnParser.Parse(File.ReadAllLines(file));
            blocksByFile[file] = blocks;

            foreach (var block in blocks)
            {
                UoxDataFixes.Apply(block);

                // UOX3 keeps the last definition of a header (scriptc.cpp overwrites defEntries[section]); files are read
                // in ordinal order so the result does not depend on the filesystem.
                if (blocksByHeader.ContainsKey(block.Header))
                {
                    error.WriteLine($"Duplicate block '[{block.Header}]' in {file}; keeping this later one, as UOX3 does.");
                }

                blocksByHeader[block.Header] = block;
            }
        }

        // Inlined for the fields only: Ids below come from each block's own lines, so an inherited name= never
        // renames a bare-hex block.
        foreach (var blocks in blocksByFile.Values)
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                var isKept = ReferenceEquals(blocksByHeader[blocks[i].Header], blocks[i]);
                blocks[i] = DfnBlockFlattener.Flatten(blocks[i], blocksByHeader);

                if (isKept)
                {
                    flatByHeader[blocks[i].Header] = blocks[i];
                }
            }
        }

        // Every block's own Id is computed once, up front, from the block alone - never from another
        // block's Id - so a get= chain or a loot entry resolves the same way no matter which order
        // the source files happen to scan in.
        var convertLoot = lootDestination is not null;
        var idByHeader = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var itemNameById = new Dictionary<string, string>(StringComparer.Ordinal);
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

                // Carried only so a loot entry can leave a human-readable Comment behind: an id
                // alone, "0x19b7", says nothing to someone reading the loot file by hand.
                if (block.Fields.TryGetValue("name", out var itemName) && itemName.Length > 0)
                {
                    itemNameById[id] = itemName;
                }
            }
        }

        // A block with no id= of its own but one parent that converted (UOX3's magic items, journals and other
        // variants: get=0x0df1 plus a name and a colour) is a template too, id = its header, inheriting the graphic.
        // Repeated until nothing changes, so a variant of a variant converts once its parent has.
        for (var added = true; added;)
        {
            added = false;

            foreach (var block in blocksByHeader.Values)
            {
                if (idByHeader.ContainsKey(block.Header) ||
                    lootIdByHeader.ContainsKey(block.Header) ||
                    !ItemTemplateBuilder.TryGetSingleParent(block, out var parent) ||
                    !idByHeader.ContainsKey(parent))
                {
                    continue;
                }

                var id = StringUtils.ToSnakeCase(block.Header);
                idByHeader[block.Header] = id;
                added = true;

                if (block.Fields.TryGetValue("name", out var itemName) && itemName.Length > 0)
                {
                    itemNameById[id] = itemName;
                }
            }
        }

        var written = 0;
        var lootWritten = 0;
        var skippedDuplicate = 0;
        var skippedNoId = 0;
        var skippedUnresolvedLootEntry = 0;

        if (convertLoot)
        {
            Directory.CreateDirectory(lootDestination!);
        }

        foreach (var (file, blocks) in blocksByFile)
        {
            var templates = new List<ItemTemplate>();
            var lootWrittenForFile = 0;

            foreach (var block in blocks)
            {
                // A header seen again later in the scan lost the "Duplicate block" warning above;
                // it must also lose the conversion, or the same Id comes out of two different
                // files (real UOX3 data does this: food/rawfoods.dfn and misc/rawfoods.dfn both
                // define [0x1e15]).
                if (!ReferenceEquals(flatByHeader[block.Header], block))
                {
                    skippedDuplicate++;

                    continue;
                }

                if (convertLoot && lootIdByHeader.TryGetValue(block.Header, out var lootId))
                {
                    var lootTemplate = LootTemplateBuilder.Build(
                        block,
                        lootId,
                        idByHeader,
                        itemNameById,
                        knownLootIds,
                        out var skipped
                    );
                    skippedUnresolvedLootEntry += skipped;

                    // Each loot table gets its own file, named after its own Id: unlike an item,
                    // reviewing or hand-editing one loot table has no reason to load every other
                    // table defined in the same source .dfn alongside it.
                    var lootOutputPath = Path.Combine(lootDestination!, lootTemplate.Id + ".toml");
                    TomlUtils.SerializeToFile(new LootTemplateFile { Loot = [lootTemplate] }, lootOutputPath);
                    lootWritten++;
                    lootWrittenForFile++;

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

                output.WriteLine(
                    $"{relative} -> {Path.GetRelativePath(destination, outputPath)} ({templates.Count} item(s))"
                );
            }

            if (lootWrittenForFile > 0)
            {
                output.WriteLine(
                    $"{relative} -> {lootWrittenForFile} loot table(s), one file each under --loot-destination"
                );
            }
        }

        output.WriteLine(
            $"Converted {written} item(s) and {lootWritten} loot table(s); skipped {skippedNoId} block(s) with no " +
            $"id= of their own, {skippedDuplicate} duplicate of an already-converted header, and " +
            $"{skippedUnresolvedLootEntry} loot entry/entries pointing at nothing this converter could resolve."
        );

        // A real read-back of what was actually written to disk, not a re-check of the resolution
        // that already ran in memory: it also catches a TOML round-trip going wrong, and a Serial
        // pair like "Base-Item"/"base_item" that would collide only after ToSnakeCase, neither of
        // which the in-memory resolution above could ever see going wrong.
        var errors = VerifyOutput(
            destination,
            convertLoot ? lootDestination : null,
            out var verifiedItems,
            out var verifiedLoot
        );

        if (ConverterOutput.ReportErrors(error, errors, "output") != 0)
        {
            return 1;
        }

        output.WriteLine(
            $"Verified {verifiedItems} item(s) and {verifiedLoot} loot table(s) read back from disk: " +
            "no duplicate ids, every BaseId and loot reference resolves."
        );

        if (mobileSource is null)
        {
            return 0;
        }

        var items = new ItemIndex(idByHeader, blocksByHeader, knownLootIds);

        var mobileResult = UoxMobileConverter.Run(
            Path.GetFullPath(mobileSource),
            Path.GetFullPath(mobileDestination!),
            Path.GetFullPath(namesDestination!),
            items,
            output,
            error
        );

        if (mobileResult != 0 || startingItemsDestination is null)
        {
            return mobileResult;
        }

        return UoxStartingItemsConverter.Run(
            Path.GetFullPath(mobileSource),
            Path.GetFullPath(startingItemsDestination),
            items,
            output,
            error
        );
    }

    /// <summary>
    ///     Reads every
    ///     <c>
    ///         .toml
    ///     </c>
    ///     file back from <paramref name="destination" /> and, when given,
    ///     <paramref name="lootDestination" />, exactly as a real loader would, and checks that no two
    ///     items or loot tables share an Id and that every <see cref="ItemTemplate.BaseId" />,
    ///     <see cref="LootEntry.ItemId" /> and <see cref="LootEntry.LootTemplateId" /> names something
    ///     that actually exists in what was written.
    /// </summary>
    private static List<string> VerifyOutput(
        string destination,
        string? lootDestination,
        out int itemCount,
        out int lootCount
    )
    {
        var errors = new List<string>();
        var items = ReadAllFromToml<ItemTemplateFile, ItemTemplate>(destination, f => f.Item);
        var itemIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (!itemIds.Add(item.Id))
            {
                errors.Add($"item '{item.Id}' is defined more than once");
            }
        }

        foreach (var item in items)
        {
            if (item.BaseId is not null && !itemIds.Contains(item.BaseId))
            {
                errors.Add($"item '{item.Id}' has BaseId '{item.BaseId}', which does not exist");
            }
        }

        itemCount = items.Count;
        lootCount = 0;

        if (lootDestination is null)
        {
            return errors;
        }

        var lootTables = ReadAllFromToml<LootTemplateFile, LootTemplate>(lootDestination, f => f.Loot);
        var lootIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var loot in lootTables)
        {
            if (!lootIds.Add(loot.Id))
            {
                errors.Add($"loot table '{loot.Id}' is defined more than once");
            }
        }

        foreach (var loot in lootTables)
        {
            foreach (var entry in loot.Entries)
            {
                if (entry.ItemId is not null && !itemIds.Contains(entry.ItemId))
                {
                    errors.Add($"loot table '{loot.Id}' has an entry with ItemId '{entry.ItemId}', which does not exist");
                }

                if (entry.LootTemplateId is not null && !lootIds.Contains(entry.LootTemplateId))
                {
                    errors.Add(
                        $"loot table '{loot.Id}' has an entry with LootTemplateId '{entry.LootTemplateId}', which does not exist"
                    );
                }
            }
        }

        lootCount = lootTables.Count;

        return errors;
    }

    private static List<TEntity> ReadAllFromToml<TFile, TEntity>(string root, Func<TFile, List<TEntity>> selectEntities)
        where TFile : class
    {
        var entities = new List<TEntity>();

        // A root that was never written to (a source with no items at all, or --loot-destination on
        // a run with no LOOTLIST blocks) is empty, not an error.
        if (!Directory.Exists(root))
        {
            return entities;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.toml", SearchOption.AllDirectories))
        {
            var file = TomlUtils.DeserializeFromFile<TFile>(path);
            entities.AddRange(selectEntities(file!));
        }

        return entities;
    }
}
