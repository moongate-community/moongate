using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Templates.Items;

/// <summary>
/// One weighted outcome inside a <see cref="LootTemplate" />. Exactly one of <see cref="ItemId" /> and
/// <see cref="LootTemplateId" /> is set for an entry that drops something; both left unset is UOX3's
/// <c>blank</c> sentinel, a real, weighted chance of dropping nothing.
/// </summary>
public class LootEntry
{
    /// <summary>
    /// This entry's share of the table, relative to every other entry's. 1, the default, is what UOX3
    /// assigns an entry with no weight of its own.
    /// </summary>
    public int Weight { get; set; } = 1;

    /// <summary>The <see cref="ItemTemplate.Id" /> to drop, when this entry resolves to a specific item.</summary>
    public string? ItemId { get; set; }

    /// <summary>
    /// The <see cref="LootTemplate.Id" /> to pick from instead, when this entry nests another table
    /// rather than naming an item directly, the way UOX3's <c>LOOTLIST=</c> entries do.
    /// </summary>
    public string? LootTemplateId { get; set; }

    /// <summary>
    /// What <see cref="ItemId" /> or <see cref="LootTemplateId" /> actually is, for a human reading this
    /// file by hand; an id alone, "0x19b7", says nothing on its own. Read by nobody at runtime.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>How many of <see cref="ItemId" /> to create. Meaningless when <see cref="LootTemplateId" /> is set instead.</summary>
    public RangeValueSpec<int> Amount { get; set; } = RangeValueSpec<int>.FromValue(1);
}
