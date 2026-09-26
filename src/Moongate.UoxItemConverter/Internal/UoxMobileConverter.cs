using Moongate.Core.Utils;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;

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

        // First every single template, so a pair can resolve both halves wherever they are defined.
        var builtByFile = blocksByFile.Select(
                                          pair => (pair.File, Built: pair.Blocks
                                                                        .Where(block => ReferenceEquals(blocksByHeader.GetValueOrDefault(block.Header), block))
                                                                        .Select(block => (Block: block, Template: MobileTemplateBuilder.Build(block, context)))
                                                                        .ToList())
                                      )
                                      .ToList();
        var byHeader = builtByFile.SelectMany(pair => pair.Built)
                                  .Where(built => built.Template is not null)
                                  .ToDictionary(built => built.Block.Header, built => built.Template!, StringComparer.OrdinalIgnoreCase);
        var byId = byHeader.Values.ToDictionary(template => template.Id);

        foreach (var (file, built) in builtByFile)
        {
            var templates = built.Select(pair => pair.Template ?? MergePair(pair.Block, byHeader, byId, report))
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

        var errors = Verify(mobileDestination, namesDestination, items, out var verifiedMobiles, out var verifiedLists);

        if (errors.Count > 0)
        {
            foreach (var verificationError in errors)
            {
                error.WriteLine($"Verification failed: {verificationError}");
            }

            error.WriteLine($"{errors.Count} verification error(s) found reading the converted mobiles back.");

            return 1;
        }

        output.WriteLine(
            $"Verified {verifiedMobiles} mobile(s) and {verifiedLists} name list(s) read back from disk: no duplicate " +
            "ids; every base_id, item, loot and name list resolves; every template validates."
        );

        return 0;
    }

    // Reads what was written back, as the server will, and checks every reference and rule.
    private static List<string> Verify(
        string mobileDestination,
        string namesDestination,
        ItemIndex items,
        out int mobileCount,
        out int listCount
    )
    {
        var errors = new List<string>();
        var listIds = (TomlUtils.DeserializeFromFile<NameListFile>(namesDestination)?.Names ?? [])
                      .Select(list => list.Id)
                      .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mobiles = Directory.Exists(mobileDestination)
            ? Directory.EnumerateFiles(mobileDestination, "*.toml", SearchOption.AllDirectories)
                       .SelectMany(path => TomlUtils.DeserializeFromFile<MobileTemplateFile>(path)?.Mobile ?? [])
                       .ToList()
            : [];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var itemIds = items.ItemIdByHeader.Values.ToHashSet(StringComparer.Ordinal);

        foreach (var mobile in mobiles.Where(mobile => !ids.Add(mobile.Id)))
        {
            errors.Add($"mobile '{mobile.Id}' is defined more than once");
        }

        foreach (var mobile in mobiles)
        {
            if (mobile.BaseId is not null && !ids.Contains(mobile.BaseId))
            {
                errors.Add($"mobile '{mobile.Id}' has base_id '{mobile.BaseId}', which does not exist");
            }

            errors.AddRange(
                (mobile.Equipment ?? []).SelectMany(entry => entry.Items)
                                        .Where(item => !itemIds.Contains(item))
                                        .Select(item => $"mobile '{mobile.Id}' equips item '{item}', which does not exist")
            );
            errors.AddRange(
                (mobile.Loot ?? []).Where(loot => !items.LootIds.Contains(loot))
                                   .Select(loot => $"mobile '{mobile.Id}' has loot '{loot}', which does not exist")
            );

            if (mobile.NameList is { } list && !list.Contains('{') && !listIds.Contains(list))
            {
                errors.Add($"mobile '{mobile.Id}' has name_list '{list}', which does not exist");
            }

            try
            {
                mobile.Validate();
            }
            catch (InvalidDataException exception)
            {
                errors.Add(exception.Message);
            }
        }

        mobileCount = mobiles.Count;
        listCount = listIds.Count;

        return errors;
    }

    // GET=a b: one template from a male/female pair; any other pair is skipped.
    private static MobileTemplate? MergePair(
        DfnBlock block,
        Dictionary<string, MobileTemplate> byHeader,
        Dictionary<string, MobileTemplate> byId,
        ConversionReport report
    )
    {
        var targets = MobileTemplateBuilder.GetTargets(block);

        if (MobileTemplateBuilder.IsSpecialSection(block.Header) || targets.Length != 2)
        {
            return null;
        }

        if (!byHeader.TryGetValue(targets[0], out var first) || !byHeader.TryGetValue(targets[1], out var second))
        {
            report.Count("two-target get, unresolved");

            return null;
        }

        return GenderPairMerger.TryMerge(
            StringUtils.ToSnakeCase(block.Header),
            first,
            second,
            template => Resolve(template, byId),
            report,
            out var merged
        )
            ? merged
            : null;
    }

    // Race and gender may come from a base: walk the base_id chain until each is found.
    private static (RaceType? Race, MobileGenderType? Gender) Resolve(MobileTemplate template, Dictionary<string, MobileTemplate> byId)
    {
        RaceType? race = null;
        MobileGenderType? gender = null;
        var seen = new HashSet<string>();

        for (var current = template; current is not null && seen.Add(current.Id); current = current.BaseId is null ? null : byId.GetValueOrDefault(current.BaseId))
        {
            race ??= current.Race;
            gender ??= current.Gender;
        }

        return (race, gender);
    }

    // Name lists are converted on their own; npclists are spawn lists, not npcs.
    private static bool IsSkippedFile(string npcDirectory, string file)
    {
        var relative = Path.GetRelativePath(npcDirectory, file).Replace('\\', '/');

        return relative.Equals("namelists.dfn", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("npclists/", StringComparison.OrdinalIgnoreCase);
    }
}
