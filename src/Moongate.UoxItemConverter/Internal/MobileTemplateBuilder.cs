using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Builds a <see cref="MobileTemplate" /> from one UOX3 npc block. Inheritance stays a <c>base_id</c>: every npc
///     block is converted, so a <c>GET</c> target always exists as a template of its own.
/// </summary>
internal static class MobileTemplateBuilder
{
    // Humanoid bodies carry the race and the gender; the race then gives the body back.
    private static readonly Dictionary<int, (RaceType Race, MobileGenderType Gender)> HumanoidBodies = new()
    {
        [0x190] = (RaceType.Human, MobileGenderType.Male),
        [0x191] = (RaceType.Human, MobileGenderType.Female),
        [0x25D] = (RaceType.Elf, MobileGenderType.Male),
        [0x25E] = (RaceType.Elf, MobileGenderType.Female),
        [0x29A] = (RaceType.Gargoyle, MobileGenderType.Male),
        [0x29B] = (RaceType.Gargoyle, MobileGenderType.Female)
    };

    /// <summary>
    ///     Gets whether <paramref name="header" /> is a section that is not an npc, such as a name list.
    /// </summary>
    public static bool IsSpecialSection(string header)
    {
        return header.StartsWith("RANDOMNAME", StringComparison.OrdinalIgnoreCase) ||
               header.StartsWith("NPCLIST", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets the <c>GET=</c> targets of a block, empty when it has none.
    /// </summary>
    public static string[] GetTargets(DfnBlock block)
    {
        return block.Fields.TryGetValue("GET", out var text)
            ? text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
    }

    /// <summary>
    ///     Builds the template, or null for a section that is not an npc or a block whose <c>GET</c> names two
    ///     targets (a gender pair, merged separately).
    /// </summary>
    public static MobileTemplate? Build(DfnBlock block, MobileBuildContext context)
    {
        if (IsSpecialSection(block.Header) || GetTargets(block).Length > 1)
        {
            return null;
        }

        var template = new MobileTemplate { Id = StringUtils.ToSnakeCase(block.Header) };

        ApplyInheritance(block, template, context);
        ApplyIdentity(block, template, context);

        return template;
    }

    private static void ApplyInheritance(DfnBlock block, MobileTemplate template, MobileBuildContext context)
    {
        // GETLBR wins over GET: LBR is UOX3's default era, the other era tags are ignored.
        var target = block.Fields.TryGetValue("GETLBR", out var eraTarget)
            ? eraTarget.Trim()
            : GetTargets(block).SingleOrDefault();

        if (target is null)
        {
            return;
        }

        if (context.MobileHeaders.Contains(target))
        {
            template.BaseId = StringUtils.ToSnakeCase(target);
        }
        else
        {
            context.Report.Count("unresolved get");
        }
    }

    private static void ApplyIdentity(DfnBlock block, MobileTemplate template, MobileBuildContext context)
    {
        if (block.Fields.TryGetValue("ID", out var idText) && TryParseNumber(idText, out var body))
        {
            if (HumanoidBodies.TryGetValue(body, out var humanoid))
            {
                (template.Race, template.Gender) = (humanoid.Race, humanoid.Gender);
            }
            else
            {
                template.Body = body;
            }
        }

        if (template.Race is null && block.Fields.TryGetValue("RACE", out var raceText))
        {
            template.Race = raceText.Trim() switch
            {
                "0" => RaceType.Human,
                "1" => RaceType.Elf,
                "2" => RaceType.Gargoyle,
                _ => null
            };
        }

        template.Name = ResolveText(block, "NAME", context);
        template.Title = ResolveText(block, "TITLE", context);

        if (block.Fields.TryGetValue("NAMELIST", out var listText) && int.TryParse(listText, out var list))
        {
            template.NameList = NameListsBuilder.ListId(list);
        }
    }

    // A number is a dictionary id; "#" means "the comment is the text"; anything else is the text itself.
    private static string? ResolveText(DfnBlock block, string key, MobileBuildContext context)
    {
        if (!block.Fields.TryGetValue(key, out var value) || value.Length == 0)
        {
            return null;
        }

        var comment = block.Comments.GetValueOrDefault(key);

        if (int.TryParse(value, out var textId))
        {
            return context.Dictionary.GetValueOrDefault(textId) ?? comment;
        }

        return value == "#" ? comment : value;
    }

    /// <summary>
    ///     Parses a UOX3 number, hex with <c>0x</c> or decimal.
    /// </summary>
    public static bool TryParseNumber(string text, out int value)
    {
        text = text.Trim();

        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value)
            : int.TryParse(text, out value);
    }
}
