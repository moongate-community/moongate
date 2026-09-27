namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Resolves a UOX3 item reference, such as <c>0x1f03</c>, <c>bagofreagents</c> or <c>listobject6</c>, to the item
///     template ids of the item pass.
/// </summary>
internal static class ItemReferences
{
    private const string ListPrefix = "listobject";

    /// <summary>
    ///     Gets the <c>[ITEMLIST n]</c> number of a <c>listobjectN</c> reference; false for any other reference.
    /// </summary>
    public static bool TryGetListNumber(string value, out int listNumber)
    {
        listNumber = 0;

        return value.StartsWith(ListPrefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(value[ListPrefix.Length..], out listNumber);
    }

    /// <summary>
    ///     Gets the ids the reference can give, one picked at random; empty, and counted, when nothing resolves.
    /// </summary>
    public static List<string> Resolve(string value, ItemIndex items, ConversionReport report)
    {
        List<string> headers;

        if (TryGetListNumber(value, out var listNumber))
        {
            if (!items.ItemBlocksByHeader.TryGetValue($"ITEMLIST {listNumber}", out var list))
            {
                report.Count("unresolved item list");

                return [];
            }

            // Lines are "weight|item" or "item"; "blank" is a chance of nothing. Items is an even pick, so the weights
            // and the blanks are dropped.
            headers = [];

            foreach (var line in list.Entries)
            {
                var item = line.Split(' ', 2)[0].Trim();
                var bar = item.IndexOf('|');

                if (bar >= 0)
                {
                    item = item[(bar + 1)..];
                    report.Count("item list weight or blank dropped");
                }

                if (item.Equals("blank", StringComparison.OrdinalIgnoreCase))
                {
                    if (bar < 0)
                    {
                        report.Count("item list weight or blank dropped");
                    }

                    continue;
                }

                headers.Add(item);
            }
        }
        else
        {
            headers = [value];
        }

        var ids = new List<string>();

        foreach (var header in headers)
        {
            var resolved = ResolveItemIds(header, items, 0);

            if (resolved.Count == 0)
            {
                report.Count("unresolved item");
            }

            ids.AddRange(resolved.Where(id => !ids.Contains(id)));
        }

        return ids;
    }

    // An item block without an id of its own is an alias: getlbr=x is x in UOX3's default era, get=a b is a or b.
    // Items already picks one evenly, so a random get becomes every target.
    private static List<string> ResolveItemIds(string header, ItemIndex items, int depth)
    {
        if (items.ItemIdByHeader.TryGetValue(header, out var id))
        {
            return [id];
        }

        if (depth > 8 || !items.ItemBlocksByHeader.TryGetValue(header, out var block))
        {
            return [];
        }

        var targets = block.Fields.TryGetValue("GETLBR", out var eraTarget)
            ? [eraTarget.Trim()]
            : MobileTemplateBuilder.GetTargets(block);

        return targets.SelectMany(target => ResolveItemIds(target, items, depth + 1)).Distinct().ToList();
    }
}
