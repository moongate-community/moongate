namespace Moongate.UO.Data.Templates.Loot;

/// <summary>
/// Serializable definition for weighted loot tables.
/// </summary>
public class LootTemplateDefinition : LootTemplateDefinitionBase
{
    /// <summary>
    /// Number of weighted rolls performed for this table.
    /// </summary>
    public int Rolls { get; set; } = 1;

    /// <summary>
    /// Relative weight for selecting no item drop (UOX3 "blank").
    /// </summary>
    public int NoDropWeight { get; set; }

    /// <summary>
    /// Weighted loot entries for this table.
    /// </summary>
    public List<LootTemplateEntry> Entries { get; set; } = [];
}
