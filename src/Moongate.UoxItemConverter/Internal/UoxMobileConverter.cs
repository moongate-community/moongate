using Moongate.Core.Utils;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Templates.Mobiles;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts UOX3 NPCs and name lists, after the item pass, against the item and loot ids it computed.
/// </summary>
internal static class UoxMobileConverter
{
    private const string NamesHeader = """
                                       # ==============================================================================
                                       # Moongate - names.toml
                                       #
                                       # What it is for:
                                       #   The lists random NPC names are drawn from. A mobile template names a list with
                                       #   name_list (for example "male", or "{gender}" for the list of the gender the
                                       #   mobile gets); the server picks one name from it at random.
                                       #
                                       # Fields:
                                       #   id      the list id, unique ignoring case
                                       #   names   the names; none may be empty
                                       #
                                       # Source: UOX3 dfndata/npc/namelists.dfn, converted by mg-uoxconv.
                                       # ==============================================================================

                                       """;

    public static int Run(
        string mobileSource,
        string mobileDestination,
        string namesDestination,
        ItemIndex items,
        TextWriter output,
        TextWriter error
    )
    {
        var dictionary = UoxDictionary.Load(Path.Combine(mobileSource, "..", "dictionaries", "dictionary.ENG"));
        var nameListsPath = Path.Combine(mobileSource, "npc", "namelists.dfn");
        var nameLists = File.Exists(nameListsPath)
            ? NameListsBuilder.Build(DfnParser.Parse(File.ReadAllLines(nameListsPath)), dictionary)
            : [];

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(namesDestination))!);
        File.WriteAllText(namesDestination, NamesHeader + TomlUtils.Serialize(new NameListFile { Names = nameLists }));
        output.WriteLine($"Converted {nameLists.Count} name list(s) to {namesDestination}.");

        var npcDirectory = Path.Combine(mobileSource, "npc");
        var sourceFiles = Directory.Exists(npcDirectory)
            ? Directory.EnumerateFiles(npcDirectory, "*.dfn", SearchOption.AllDirectories)
                       .Where(file => !IsSkippedFile(npcDirectory, file))
                       .Order(StringComparer.Ordinal)
                       .ToArray()
            : [];
        var blocksByFile = new List<(string File, List<DfnBlock> Blocks)>();
        var blocksByHeader = new Dictionary<string, DfnBlock>(StringComparer.OrdinalIgnoreCase);
        var report = new ConversionReport();

        foreach (var file in sourceFiles)
        {
            var blocks = DfnParser.Parse(File.ReadAllLines(file));
            blocksByFile.Add((file, blocks));

            foreach (var block in blocks.Where(block => !MobileTemplateBuilder.IsSpecialSection(block.Header)))
            {
                if (!blocksByHeader.TryAdd(block.Header, block))
                {
                    report.Count("duplicate npc header");
                }
            }
        }

        var context = new MobileBuildContext(
            dictionary,
            items,
            blocksByHeader.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            UoxColorLists.Load(Path.Combine(mobileSource, "colors", "colors.dfn")),
            UoxCreatureSounds.Load(Path.Combine(mobileSource, "creatures", "creatures.dfn")),
            report
        );
        var written = 0;

        foreach (var (file, blocks) in blocksByFile)
        {
            var templates = blocks.Where(block => ReferenceEquals(blocksByHeader.GetValueOrDefault(block.Header), block))
                                  .Select(block => MobileTemplateBuilder.Build(block, context))
                                  .OfType<MobileTemplate>()
                                  .ToList();

            if (templates.Count == 0)
            {
                continue;
            }

            var relative = Path.GetRelativePath(npcDirectory, file);
            var outputPath = Path.Combine(mobileDestination, Path.ChangeExtension(relative, ".toml"));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            TomlUtils.SerializeToFile(new MobileTemplateFile { Mobile = templates }, outputPath);
            written += templates.Count;
            output.WriteLine($"npc/{relative} -> {Path.GetRelativePath(mobileDestination, outputPath)} ({templates.Count} mobile(s))");
        }

        output.WriteLine($"Converted {written} mobile(s).");

        foreach (var (reason, count) in report.Lines)
        {
            output.WriteLine($"  {count} x {reason}");
        }

        return 0;
    }

    // Name lists are converted on their own; npclists are spawn lists, not npcs.
    private static bool IsSkippedFile(string npcDirectory, string file)
    {
        var relative = Path.GetRelativePath(npcDirectory, file).Replace('\\', '/');

        return relative.Equals("namelists.dfn", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("npclists/", StringComparison.OrdinalIgnoreCase);
    }
}
