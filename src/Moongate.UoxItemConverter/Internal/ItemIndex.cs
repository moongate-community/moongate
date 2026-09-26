namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     What the item pass computed that the mobile pass resolves against: every item block's template id by header,
///     every item-source block by header (for <c>[ITEMLIST n]</c>), and every loot table id.
/// </summary>
internal sealed record ItemIndex(
    IReadOnlyDictionary<string, string> ItemIdByHeader,
    IReadOnlyDictionary<string, DfnBlock> ItemBlocksByHeader,
    IReadOnlySet<string> LootIds
);
