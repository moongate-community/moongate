namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Corrects known mistakes in UOX3's own item data as the blocks are read, so the converted templates show what
///     UOX3 meant.
/// </summary>
internal static class UoxDataFixes
{
    // (header, tag) → the corrected value.
    private static readonly Dictionary<(string Header, string Tag), string> Fixes = new()
    {
        // leather.dfn names the leather gloves (0x13c6) and a leather tunic (0x13cc) for these two.
        [("necro_sleeves", "get")] = "0x13cd",
        [("necro_leggings", "get")] = "0x13cb"
    };

    /// <summary>
    ///     Applies the corrections for <paramref name="block" />'s header, if any, to its fields.
    /// </summary>
    public static void Apply(DfnBlock block)
    {
        foreach (var ((header, tag), value) in Fixes)
        {
            if (header.Equals(block.Header, StringComparison.OrdinalIgnoreCase))
            {
                block.Fields[tag] = value;
            }
        }
    }
}
