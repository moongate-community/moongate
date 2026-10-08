using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Types.Guilds;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts the guildmasters of ModernUO (<c>Mobiles/Vendors/NPC/Guildmasters/*Guildmaster.cs</c>) into mobile
///     templates: for each class a man, <c>m_&lt;trade&gt;_guildmaster</c>, and a woman, <c>f_&lt;trade&gt;_guildmaster</c>
///     (UOX3's names), with the title, the skills (the C# ranges as dice), the guild and the clothes of a vendor, and the
///     npc list <c>&lt;trade&gt;guildmaster</c> that picks one of the two. Nothing is run: the C# is read as syntax.
/// </summary>
internal static class ModernUoGuildmasterConverter
{
    private const string MobilesFile = "guildmasters.toml";
    private const string ListsFile = "npclists_guildmasters.toml";
    private const string Suffix = "Guildmaster";
    private const string BaseClass = "BaseGuildmaster";
    private const string Mobiles = "Mobiles";

    private const string MobileHeader =
        """
        # What it is for:
        #   The guildmasters of the trades: a vendor-like NPC with the skills of its trade at 90 or more, who teaches them
        #   and takes members for the guild of the trade (npc_guild). One man and one woman for each trade; the npc list
        #   of the same name, without the sex, picks one of them.
        #
        # Fields:
        #   see templates.md; npc_guild is the guild the NPC takes members for: a player says join to it and pays 500 gold

        """;

    private const string ListHeader =
        """
        # What it is for:
        #   One npc list for each trade of guildmaster, with its man and its woman, so that a spawn names the trade only.
        #
        # Fields:
        #   see templates.md

        """;

    // The skills of ModernUO under the names of this server.
    private static readonly Dictionary<string, SkillType> Skills = new(StringComparer.Ordinal)
    {
        ["Blacksmith"] = SkillType.Blacksmithy,
        ["DetectHidden"] = SkillType.DetectingHidden,
        ["EvalInt"] = SkillType.EvaluatingIntelligence,
        ["Forensics"] = SkillType.ForensicEvaluation,
        ["Inscribe"] = SkillType.Inscription,
        ["ItemID"] = SkillType.ItemIdentification,
        ["Macing"] = SkillType.MaceFighting,
        ["MagicResist"] = SkillType.ResistingSpells,
        ["Parry"] = SkillType.Parrying,
        ["Swords"] = SkillType.Swordsmanship
    };

    // The guild of ModernUO's NpcGuild enum, under the name of this server.
    private static readonly Dictionary<string, NpcGuildType> Guilds = new(StringComparer.Ordinal)
    {
        ["MagesGuild"] = NpcGuildType.Mages,
        ["WarriorsGuild"] = NpcGuildType.Warriors,
        ["ThievesGuild"] = NpcGuildType.Thieves,
        ["RangersGuild"] = NpcGuildType.Rangers,
        ["HealersGuild"] = NpcGuildType.Healers,
        ["MinersGuild"] = NpcGuildType.Miners,
        ["MerchantsGuild"] = NpcGuildType.Merchants,
        ["TinkersGuild"] = NpcGuildType.Tinkers,
        ["TailorsGuild"] = NpcGuildType.Tailors,
        ["FishermensGuild"] = NpcGuildType.Fishermen,
        ["BardsGuild"] = NpcGuildType.Bards,
        ["BlacksmithsGuild"] = NpcGuildType.Blacksmiths
    };

    public static int Run(string source, string mobiles, string npcLists, TextWriter output, TextWriter error)
    {
        var root = Directory.Exists(Path.Combine(source, Mobiles, "Vendors", "NPC", "Guildmasters"))
            ? Path.Combine(source, Mobiles, "Vendors", "NPC", "Guildmasters")
            : source;

        if (!Directory.Exists(root))
        {
            error.WriteLine($"ModernUO guildmasters folder does not exist: {root}");

            return 2;
        }

        try
        {
            var report = new ConversionReport();
            var masters = new List<MobileTemplate>();
            var lists = new List<NpcListTemplate>();

            foreach (var path in Directory.EnumerateFiles(root, "*" + Suffix + ".cs").Order(StringComparer.Ordinal))
            {
                if (Path.GetFileNameWithoutExtension(path) == BaseClass)
                {
                    continue;
                }

                var master = Read(File.ReadAllText(path, new UTF8Encoding(false, true)), path, report);

                if (master is null)
                {
                    continue;
                }

                var (man, woman, list) = Build(master);
                masters.AddRange([man, woman]);
                lists.Add(list);
            }

            if (masters.Count == 0)
            {
                error.WriteLine($"{root}: no guildmaster found.");

                return 2;
            }

            ConverterOutput.WriteToml(Path.Combine(mobiles, MobilesFile), MobileHeader, new MobileTemplateFile { Mobile = masters });
            ConverterOutput.WriteToml(Path.Combine(npcLists, ListsFile), ListHeader, new NpcListTemplateFile { NpcList = lists });
            output.WriteLine($"mobiles/{MobilesFile} ({masters.Count} templates), npc_lists/{ListsFile} ({lists.Count} lists)");
            ConverterOutput.WriteReport(output, report);

            return 0;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException
                                              or UnauthorizedAccessException or DecoderFallbackException)
        {
            error.WriteLine($"Guildmaster conversion failed: {exception.Message}");

            return 2;
        }
    }

    private static Guildmaster? Read(string text, string path, ConversionReport report)
    {
        var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview), path);

        if (tree.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) is { } fault)
        {
            throw new InvalidDataException(fault.ToString());
        }

        var owner = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var constructor = owner?.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();

        if (owner is null ||
            constructor?.Initializer?.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax
            {
                Token.Value: string title
            })
        {
            report.Count($"{Path.GetFileName(path)} has no constructor with a title");

            return null;
        }

        var guild = owner.Members.OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(property => property.Identifier.ValueText == "NpcGuild")
            ?.ExpressionBody?.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: var guildName } &&
                    Guilds.TryGetValue(guildName, out var known)
            ? known
            : (NpcGuildType?)null;

        if (guild is null)
        {
            // ModernUO's miner guildmaster names no guild: it teaches, and takes no members.
            report.Count($"{owner.Identifier.ValueText} names no guild: it teaches only");
        }

        var skills = new Dictionary<string, DiceSpec>(StringComparer.Ordinal);

        foreach (var call in constructor.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not IdentifierNameSyntax { Identifier.ValueText: "SetSkill" } ||
                call.ArgumentList.Arguments is not [var name, var low, var high] ||
                name.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var skillName })
            {
                continue;
            }

            if (!Skills.TryGetValue(skillName, out var skill) && !Enum.TryParse(skillName, out skill))
            {
                report.Count($"skill {skillName} is not one of this server's");

                continue;
            }

            if (low.Expression is LiteralExpressionSyntax { Token.Value: double min } &&
                high.Expression is LiteralExpressionSyntax { Token.Value: double max } &&
                MobileTemplateBuilder.ToDice($"{(int)min} {(int)max}") is { } dice)
            {
                skills[StringUtils.ToSnakeCase(skill.ToString())] = dice;
            }
        }

        return new(owner.Identifier.ValueText, title, guild, skills);
    }

    private static (MobileTemplate Man, MobileTemplate Woman, NpcListTemplate List) Build(Guildmaster master)
    {
        var trade = StringUtils.ToSnakeCase(master.Title);
        var man = Template(master, trade, "m", MobileGenderType.Male, "guildmaster", 348, Clothes(false));
        var woman = Template(master, trade, "f", MobileGenderType.Female, "guildmistress", 337, Clothes(true));
        var list = new NpcListTemplate
        {
            Id = trade + "guildmaster",
            Entries = [new() { MobileId = man.Id }, new() { MobileId = woman.Id }]
        };

        return (man, woman, list);
    }

    private static MobileTemplate Template(
        Guildmaster master,
        string trade,
        string prefix,
        MobileGenderType gender,
        string word,
        int death,
        List<MobileEquipmentEntry> outfit
    )
    {
        return new()
        {
            Id = $"{prefix}_{trade}_guildmaster",
            BaseId = "basevendor",
            NameList = gender == MobileGenderType.Male ? "male" : "female",
            Title = $"the {master.Title} {word}",
            Gender = gender,
            Race = RaceType.Human,
            NpcGuild = master.Guild,
            Skills = master.Skills,
            Sounds = new() { Death = death },
            Equipment = outfit
        };
    }

    // The clothes of the vendors of this server: a shirt, trousers or a skirt, shoes and an apron or a dress.
    private static List<MobileEquipmentEntry> Clothes(bool woman)
    {
        var hue = HueSpec.Parse("0x0835-0x0852");

        return
        [
            new() { Items = ["0x1517_shirt"], Hue = hue },
            new()
            {
                Items = woman
                    ? ["0x1516", "0x152e_short_pants", "0x1537_kilt", "0x1539_long_pants"]
                    : ["0x152e_short_pants", "0x1539_long_pants"],
                Hue = hue
            },
            new() { Items = ["0x170b_boots", "0x170d_sandals", "0x170f_shoes", "0x1711_thigh_boots"] },
            new()
            {
                Items = woman
                    ? ["0x1fa1_tunic", "0x1f01_plain_dress", "0x153b_half_apron", "0x153d_full_apron"]
                    : ["0x1fa1_tunic", "0x153b_half_apron", "0x153d_full_apron"],
                Hue = hue
            }
        ];
    }

    private sealed record Guildmaster(string Class, string Title, NpcGuildType? Guild, Dictionary<string, DiceSpec> Skills);
}
