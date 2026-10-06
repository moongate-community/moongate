using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Turns the <c>[BESTSKILL n]</c> and <c>[DEFAULT ...]</c> blocks of UOX3's <c>newbie.dfn</c> into
///     <see cref="StartingItemSet" />s, and adds Moongate's own gold, bread and water to the common set.
/// </summary>
internal static class StartingItemsBuilder
{
    private const string SkillPrefix = "BESTSKILL ";

    private const string CommonHeader = "DEFAULT ALL";

    // What UOX3 does not keep in newbie.dfn and every new character of Moongate gets: the starting gold, which
    // UOX3 reads from uox.ini (STARTGOLD), and something against hunger and thirst.
    private const string GoldHeader = "0x0eed";
    private const int StartingGold = 1000;
    private const string BreadHeader = "0x103b";
    private const int StartingBread = 3;
    private const string WaterHeader = "0x1f9e";

    // UOX3 picks the DEFAULT section by body: MALE and FEMALE are the human bodies only.
    private static readonly Dictionary<string, (RaceType Race, GenderType Gender)> Defaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DEFAULT MALE"] = (RaceType.Human, GenderType.Male),
            ["DEFAULT FEMALE"] = (RaceType.Human, GenderType.Female),
            ["DEFAULT ELF MALE"] = (RaceType.Elf, GenderType.Male),
            ["DEFAULT ELF FEMALE"] = (RaceType.Elf, GenderType.Female),
            ["DEFAULT GARG MALE"] = (RaceType.Gargoyle, GenderType.Male),
            ["DEFAULT GARG FEMALE"] = (RaceType.Gargoyle, GenderType.Female)
        };

    /// <summary>
    ///     Builds one set per block that gives at least one item, in the order of the blocks.
    /// </summary>
    public static List<StartingItemSet> Build(IEnumerable<DfnBlock> blocks, ItemIndex items, ConversionReport report)
    {
        var sets = new List<StartingItemSet>();

        foreach (var block in blocks)
        {
            StartingItemSet set;

            if (block.Header.Equals(CommonHeader, StringComparison.OrdinalIgnoreCase))
            {
                set = new() { Common = true };
            }
            else if (Defaults.TryGetValue(block.Header, out var filter))
            {
                set = new() { Race = filter.Race, Gender = filter.Gender };
            }
            else if (block.Header.StartsWith(SkillPrefix, StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(block.Header[SkillPrefix.Length..].Trim(), out var skill) &&
                     Enum.IsDefined((SkillType)skill))
            {
                set = new() { Skill = FromUoxSkillNumber(skill) };
            }
            else
            {
                continue;
            }

            foreach (var line in block.Entries)
            {
                if (BuildEntry(line, items, report) is { } entry)
                {
                    set.Items.Add(entry);
                }
            }

            if (set.Items.Count > 0)
            {
                sets.Add(set);
            }
        }

        AddOwnItems(sets, items);

        return sets;
    }

    // Moongate's own entries of the common set, for the items the source has: the gold first, food and drink last.
    private static void AddOwnItems(List<StartingItemSet> sets, ItemIndex items)
    {
        var gold = OwnEntry(items, GoldHeader, StartingGold);
        var food = new[] { OwnEntry(items, BreadHeader, StartingBread), OwnEntry(items, WaterHeader, 1) }
            .OfType<StartingItemEntry>()
            .ToList();

        if (gold is null && food.Count == 0)
        {
            return;
        }

        var common = sets.FirstOrDefault(set => set.Common);

        if (common is null)
        {
            common = new() { Common = true };
            sets.Add(common);
        }

        if (gold is not null)
        {
            common.Items.Insert(0, gold);
        }

        common.Items.AddRange(food);
    }

    private static StartingItemEntry? OwnEntry(ItemIndex items, string header, int amount)
    {
        if (!items.ItemIdByHeader.TryGetValue(header, out var id))
        {
            return null;
        }

        return new() { Items = [id], Amount = amount == 1 ? null : DiceSpec.FromValue(amount) };
    }

    // PACKITEM=item[,amount[,newbie]] and EQUIPITEM=item[,hue[,newbie]].
    private static StartingItemEntry? BuildEntry(string line, ItemIndex items, ConversionReport report)
    {
        var separator = line.IndexOf('=');

        if (separator < 0)
        {
            return null;
        }

        var key = line[..separator].Trim().ToUpperInvariant();

        if (key is not ("PACKITEM" or "EQUIPITEM"))
        {
            report.Count($"unknown newbie tag {key}");

            return null;
        }

        var parts = line[(separator + 1)..].Split(',', StringSplitOptions.TrimEntries);
        var ids = ItemReferences.Resolve(parts[0], items, report);

        if (ids.Count == 0)
        {
            return null;
        }

        var entry = new StartingItemEntry { Items = ids, Equip = key == "EQUIPITEM" };

        if (parts.Length >= 2 && UoxNumber.TryParse(parts[1], out var value))
        {
            if (entry.Equip)
            {
                entry.Hue = HueSpec.FromValue(value);
            }
            else if (value != 1)
            {
                entry.Amount = DiceSpec.FromValue(value);
            }
        }

        if (parts.Length >= 3 && UoxNumber.TryParse(parts[2], out var newbie))
        {
            entry.Newbie = newbie != 0;
        }

        return entry;
    }

    // UOX3 numbers IMBUING 55 and MYSTICISM 56 (enums.h); SkillType has them the other way round. Every other number
    // matches.
    private static SkillType FromUoxSkillNumber(int number)
    {
        return number switch
        {
            55 => SkillType.Imbuing,
            56 => SkillType.Mysticism,
            _  => (SkillType)number
        };
    }
}
