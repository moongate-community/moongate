using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Builds a <see cref="LootTemplate" /> from one
///     <c>
///         [LOOTLIST name]
///     </c>
///     block, resolving each
///     entry's item or nested-table reference against the same maps <see cref="ItemTemplateBuilder" /> uses.
/// </summary>
internal static class LootTemplateBuilder
{
    private const string HeaderPrefix = "LOOTLIST ";
    private const string NestedLootPrefix = "LOOTLIST=";
    private const string NestedItemListPrefix = "ITEMLIST=";

    /// <summary>
    ///     True when <paramref name="header" /> names a loot block (
    ///     <c>
    ///         "LOOTLIST name"
    ///     </c>
    ///     ),
    ///     with the table's own Id, everything after the prefix run through <see cref="StringUtils.ToSnakeCase" />
    ///     (real names are camelCase, "eartheleLoot"), as <paramref name="lootId" />.
    /// </summary>
    public static bool TryGetLootId(string header, out string lootId)
    {
        if (header.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            lootId = StringUtils.ToSnakeCase(header[HeaderPrefix.Length..].Trim());

            return true;
        }

        lootId = "";

        return false;
    }

    public static LootTemplate Build(
        DfnBlock block,
        string id,
        IReadOnlyDictionary<string, string> idByHeader,
        IReadOnlyDictionary<string, string> itemNameById,
        IReadOnlySet<string> knownLootIds,
        out int skippedEntries
    )
    {
        var entries = new List<LootEntry>();
        skippedEntries = 0;

        foreach (var rawLine in block.Entries)
        {
            var entry = ParseEntry(rawLine, idByHeader, itemNameById, knownLootIds);

            if (entry is null)
            {
                skippedEntries++;

                continue;
            }

            entries.Add(entry);
        }

        return new() { Id = id, Entries = entries };
    }

    private static LootEntry? ParseEntry(
        string rawLine,
        IReadOnlyDictionary<string, string> idByHeader,
        IReadOnlyDictionary<string, string> itemNameById,
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
            return new() { Weight = weight, Amount = amount };
        }

        if (reference.StartsWith(NestedLootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var nestedId = StringUtils.ToSnakeCase(reference[NestedLootPrefix.Length..].Trim());

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

        if (!idByHeader.TryGetValue(reference, out var itemId))
        {
            return null;
        }

        itemNameById.TryGetValue(itemId, out var comment);

        return new() { Weight = weight, ItemId = itemId, Comment = comment, Amount = amount };
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

        return int.TryParse(parts[0], out var value)
            ? RangeValueSpec<int>.FromValue(value)
            : RangeValueSpec<int>.FromValue(1);
    }
}
