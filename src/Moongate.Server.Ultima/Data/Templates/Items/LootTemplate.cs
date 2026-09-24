namespace Moongate.Server.Ultima.Data.Templates.Items;

/// <summary>
/// A weighted loot table, its own file under <c>templates/loots/</c>. Picking one entry from
/// <see cref="Entries" /> is the loader's job, not this type's.
/// </summary>
public class LootTemplate
{
    /// <summary>The stable id an <see cref="LootEntry.LootTemplateId" /> or an NPC's death loot names this table by.</summary>
    public string Id { get; set; }

    /// <summary>A designer's note. Read by nobody at runtime.</summary>
    public string? Comment { get; set; }

    /// <summary>The table's weighted outcomes.</summary>
    public List<LootEntry> Entries { get; set; } = [];
}
