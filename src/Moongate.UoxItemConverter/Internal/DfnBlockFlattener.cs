namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Inlines a
///     <c>
///         get=
///     </c>
///     target that has no
///     <c>
///         id=
///     </c>
///     of its own, such as
///     <c>
///         [base_coin]
///     </c>
///     , into the block that names it. UOX3 applies such a target's lines in place, so its fields
///     (a coin's
///     <c>
///         weight=2
///     </c>
///     ,
///     <c>
///         pileable=1
///     </c>
///     ) would otherwise never reach the templates made from its children. A target with an
///     <c>
///         id=
///     </c>
///     is converted on its own and stays a
///     <c>
///         BaseId
///     </c>
///     .
/// </summary>
internal static class DfnBlockFlattener
{
    public static DfnBlock Flatten(DfnBlock block, IReadOnlyDictionary<string, DfnBlock> blocksByHeader)
    {
        return Flatten(block, blocksByHeader, new(StringComparer.OrdinalIgnoreCase));
    }

    private static DfnBlock Flatten(
        DfnBlock block,
        IReadOnlyDictionary<string, DfnBlock> blocksByHeader,
        HashSet<string> visiting
    )
    {
        // get=a b is a random pick among aliases, not a parent: it is left to the builder.
        if (!block.Fields.TryGetValue("get", out var getText) ||
            getText.Split(' ', StringSplitOptions.RemoveEmptyEntries) is not [var target] ||
            !blocksByHeader.TryGetValue(target, out var parent) ||
            ItemTemplateBuilder.TryComputeId(parent, out _, out _) ||
            !visiting.Add(block.Header))
        {
            return block;
        }

        var flatParent = Flatten(parent, blocksByHeader, visiting);
        visiting.Remove(block.Header);

        // The child's own lines win, as they come after get= in UOX3. The child's get= is replaced by
        // whatever its parent resolved to, so an id= ancestor further up still becomes the BaseId.
        var fields = new Dictionary<string, string>(flatParent.Fields, StringComparer.OrdinalIgnoreCase);
        fields.Remove("get");

        foreach (var (key, value) in block.Fields)
        {
            if (!key.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                fields[key] = value;
            }
        }

        if (flatParent.Fields.TryGetValue("get", out var parentGet))
        {
            fields["get"] = parentGet;
        }

        var entries = flatParent.Entries
                                .Where(line => !IsGetLine(line))
                                .Concat(block.Entries.Where(line => !IsGetLine(line)))
                                .ToList();

        return block with { Fields = fields, Entries = entries };
    }

    private static bool IsGetLine(string line)
    {
        var separator = line.IndexOf('=');

        return separator >= 0 && line[..separator].Trim().Equals("get", StringComparison.OrdinalIgnoreCase);
    }
}
