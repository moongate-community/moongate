using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Templates.Loot;

/// <summary>
/// Serializable definition for loot tables.
/// </summary>
public class LootTemplateDefinition : LootTemplateDefinitionBase
{
    /// <summary>
    /// Defines how entries are interpreted when loot is generated.
    /// </summary>
    public LootTemplateMode Mode { get; set; } = LootTemplateMode.Weighted;

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
