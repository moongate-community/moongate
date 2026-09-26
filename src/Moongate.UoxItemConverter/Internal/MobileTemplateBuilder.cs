using System.Globalization;
using Moongate.Core.Primitives;
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
        ApplyNumbers(block, template, context);

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
    ///     Turns UOX3's <c>lo hi</c> or single value into dice: one value is a constant, two are one die spanning them
    ///     (<c>96 120</c> is <c>1d25+95</c>). Each value is divided by <paramref name="divisor" /> (skills are in
    ///     tenths) and clamped to <paramref name="max" />. Null when the text is not numbers.
    /// </summary>
    public static DiceSpec? ToDice(string text, int divisor = 1, int max = int.MaxValue)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 2 || !parts.All(part => TryParseNumber(part, out _)))
        {
            return null;
        }

        var values = parts.Select(part => Math.Min(ParseNumber(part) / divisor, max)).ToArray();
        var (low, high) = (Math.Min(values[0], values[^1]), Math.Max(values[0], values[^1]));

        if (low == high)
        {
            return DiceSpec.FromValue(low);
        }

        var offset = low - 1;
        var bonus = offset switch
        {
            0 => "",
            > 0 => $"+{offset}",
            _ => offset.ToString(CultureInfo.InvariantCulture)
        };

        return DiceSpec.Parse($"1d{high - low + 1}{bonus}");
    }

    private static void ApplyNumbers(DfnBlock block, MobileTemplate template, MobileBuildContext context)
    {
        // UOX3 applies tags in file order, so a later line wins; HPMAX beats HP wherever it is.
        foreach (var entry in block.Entries)
        {
            var separator = entry.IndexOf('=');

            if (separator < 0)
            {
                continue;
            }

            var key = entry[..separator].Trim().ToUpperInvariant();
            var value = entry[(separator + 1)..].Trim();

            switch (key)
            {
                case "STR" or "ST" or "STRENGTH":
                    template.Strength = Dice(value, context);

                    break;
                case "DEX" or "DX" or "DEXTERITY":
                    template.Dexterity = Dice(value, context);

                    break;
                case "INT" or "IN" or "INTELLIGENCE":
                    template.Intelligence = Dice(value, context);

                    break;
                case "HPMAX":
                    template.Hits = Dice(value, context);

                    break;
                case "HP" when !block.Fields.ContainsKey("HPMAX"):
                    template.Hits = Dice(value, context);

                    break;
                case "MANAMAX":
                    template.Mana = Dice(value, context);

                    break;
                case "MANA" when !block.Fields.ContainsKey("MANAMAX"):
                    template.Mana = Dice(value, context);

                    break;
                case "STAMINAMAX":
                    template.Stamina = Dice(value, context);

                    break;
                case "STAMINA" when !block.Fields.ContainsKey("STAMINAMAX"):
                    template.Stamina = Dice(value, context);

                    break;
                case "DAMAGE" or "ATT":
                    template.Damage = Dice(value, context);

                    break;
                case "DEF":
                    template.Armor = Dice(value, context);

                    break;
                case "RESISTFIRE":
                    (template.Resistances ??= new()).Fire = Dice(value, context);

                    break;
                case "RESISTCOLD":
                    (template.Resistances ??= new()).Cold = Dice(value, context);

                    break;
                case "RESISTPOISON":
                    (template.Resistances ??= new()).Poison = Dice(value, context);

                    break;
                case "RESISTLIGHTNING":
                    (template.Resistances ??= new()).Energy = Dice(value, context);

                    break;
                case "ELEMENTRESIST":
                    ApplyElementResist(value, template, context);

                    break;
                case "KARMA":
                    template.Karma = Dice(value, context);

                    break;
                case "FAME":
                    template.Fame = Dice(value, context);

                    break;
                case "GOLD":
                    template.Gold = Dice(value, context);

                    break;
                case "FLAG":
                    template.Notoriety = value.ToUpperInvariant() switch
                    {
                        "INNOCENT" => NotorietyType.Innocent,
                        "NEUTRAL" => NotorietyType.Attackable,
                        "EVIL" => NotorietyType.Murderer,
                        _ => template.Notoriety
                    };

                    break;
                case "CUSTOMINTTAG" or "CUSTOMSTRINGTAG":
                    ApplyTag(value, template);

                    break;
                default:
                    if (UoxSkillNames.TryMap(key, out var skill))
                    {
                        if (Dice(value, context, 10, 120) is { } points)
                        {
                            (template.Skills ??= new())[EnumNameUtils.Format(skill)] = points;
                        }
                    }

                    break;
            }
        }
    }

    // ELEMENTRESIST=heat cold lightning poison.
    private static void ApplyElementResist(string value, MobileTemplate template, MobileBuildContext context)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 4 || !parts.All(part => TryParseNumber(part, out _)))
        {
            context.Report.Count("bad number");

            return;
        }

        var resistances = template.Resistances ??= new();
        resistances.Fire = DiceSpec.FromValue(ParseNumber(parts[0]));
        resistances.Cold = DiceSpec.FromValue(ParseNumber(parts[1]));
        resistances.Energy = DiceSpec.FromValue(ParseNumber(parts[2]));
        resistances.Poison = DiceSpec.FromValue(ParseNumber(parts[3]));
    }

    private static void ApplyTag(string value, MobileTemplate template)
    {
        var parts = value.Split(' ', 2, StringSplitOptions.TrimEntries);

        if (parts.Length == 2 && parts[0].Length > 0)
        {
            (template.Tags ??= new())[parts[0]] = parts[1];
        }
    }

    private static DiceSpec? Dice(string value, MobileBuildContext context, int divisor = 1, int max = int.MaxValue)
    {
        var dice = ToDice(value, divisor, max);

        if (dice is null)
        {
            context.Report.Count("bad number");
        }

        return dice;
    }

    private static int ParseNumber(string text)
    {
        TryParseNumber(text, out var value);

        return value;
    }

    /// <summary>
    ///     Parses a UOX3 number, hex with <c>0x</c> or decimal.
    /// </summary>
    public static bool TryParseNumber(string text, out int value)
    {
        text = text.Trim();

        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text[2..], NumberStyles.HexNumber, null, out value)
            : int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
