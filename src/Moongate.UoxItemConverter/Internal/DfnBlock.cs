namespace Moongate.UoxItemConverter.Internal;

/// <summary>
/// One <c>[header] { ... }</c> block read from a UOX3 <c>.dfn</c> file: <see cref="Fields" />
/// for an item block's flat <c>key=value</c> lines, <see cref="Entries" /> for every line verbatim,
/// which is what a <c>[LOOTLIST ...]</c> block's bare, unkeyed entry lines need instead.
/// </summary>
internal sealed record DfnBlock(string Header, Dictionary<string, string> Fields, List<string> Entries);
