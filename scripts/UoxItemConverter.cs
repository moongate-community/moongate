#:project ../src/Moongate.Server.Ultima/Moongate.Server.Ultima.csproj

// Converts UOX3 item definitions (github.com/UOX3DevTeam/UOX3, data/dfndata/items/**/*.dfn) into
// Moongate ItemTemplate TOML files. UOX3, not POL: UOX3's get= chains one item off another
// (get=base_item, get=0x1440), which maps directly onto ItemTemplate.BaseId; POL has no equivalent,
// so a POL source would leave BaseId empty for everything.
//
// Usage:
//   dotnet run --file scripts/UoxItemConverter.cs -- --source <file-or-directory> --destination <dir>
//
// --source is a single .dfn file or a directory scanned recursively for *.dfn files. Every block is
// read from every source file before any get= chain is resolved, since a chain's target can live in
// a different file than the block that names it (base_item and base_cutlass do, in real UOX3 data).
// One <name>.toml is written per source .dfn, alongside the source's own relative path under
// --destination, each holding one [[item]] per convertible block.
//
// What converts, and what does not:
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

using System.Text;
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

        var idByHeader = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var written = 0;
        var skippedDuplicate = 0;
        var skippedNoId = 0;

        foreach (var (file, blocks) in blocksByFile)
        {
            var templates = new List<ItemTemplate>();

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

                var template = ItemTemplateBuilder.Build(block, blocksByHeader, idByHeader);

                if (template is null)
                {
                    skippedNoId++;

                    continue;
                }

                idByHeader[block.Header] = template.Id;
                templates.Add(template);
            }

            if (templates.Count == 0)
            {
                continue;
            }

            var relative = Path.GetRelativePath(Directory.Exists(source) ? source : Path.GetDirectoryName(source)!, file);
            var outputPath = Path.Combine(destination, Path.ChangeExtension(relative, ".toml"));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            TomlUtils.SerializeToFile(new ItemTemplateFile { Item = templates }, outputPath);
            written += templates.Count;

            Console.WriteLine($"{relative} -> {Path.GetRelativePath(destination, outputPath)} ({templates.Count} item(s))");
        }

        Console.WriteLine(
            $"Converted {written} item(s); skipped {skippedNoId} block(s) with no id= of their own " +
            $"and {skippedDuplicate} duplicate of an already-converted header."
        );

        return 0;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            """
            Usage: dotnet run --file scripts/UoxItemConverter.cs -- --source <file-or-directory> --destination <dir>

              --source       A single .dfn file, or a directory scanned recursively for *.dfn files.
              --destination  Directory to write the converted ItemTemplate .toml files under.
            """
        );
    }
}

/// <summary>One <c>[header] { key=value ... }</c> block read from a UOX3 <c>.dfn</c> file.</summary>
internal sealed record DfnBlock(string Header, Dictionary<string, string> Fields);

/// <summary>Reads UOX3's <c>.dfn</c> block format: <c>// comment</c> lines, blank lines, a
/// <c>[header]</c> line, a bare <c>{</c>, flat <c>key=value</c> lines, and a bare <c>}</c>.</summary>
internal static class DfnParser
{
    public static List<DfnBlock> Parse(IReadOnlyList<string> lines)
    {
        var blocks = new List<DfnBlock>();
        string? header = null;
        Dictionary<string, string>? fields = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                header = line[1..^1];
                fields = null;

                continue;
            }

            if (line == "{")
            {
                fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                continue;
            }

            if (line == "}")
            {
                if (header is not null && fields is not null)
                {
                    blocks.Add(new DfnBlock(header, fields));
                }

                header = null;
                fields = null;

                continue;
            }

            if (fields is null)
            {
                continue;
            }

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
/// target against templates already built from earlier blocks.</summary>
internal static class ItemTemplateBuilder
{
    public static ItemTemplate? Build(
        DfnBlock block,
        IReadOnlyDictionary<string, DfnBlock> blocksByHeader,
        IReadOnlyDictionary<string, string> idByHeader
    )
    {
        if (!block.Fields.TryGetValue("id", out var idText) || !Serial.TryParse(idText, out var itemId))
        {
            return null;
        }

        // The header alone is always unique (duplicates are caught and warned about while every
        // block is being read). name= is not: UOX3 reuses it across many facing, material or
        // damage-state variants of the same conceptual thing, sometimes literally "#", so it can
        // only ever be an addition to the header, never a replacement for it.
        var id = IsBareHex(block.Header) && block.Fields.TryGetValue("name", out var name) && name.Length > 0
                     ? $"{block.Header}_{name}"
                     : block.Header;

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

/// <summary>The root of one converted TOML file: an array of tables under the key <c>item</c>.</summary>
internal sealed class ItemTemplateFile
{
    public List<ItemTemplate> Item { get; set; } = [];
}
