namespace Moongate.UO.Data.Templates.Loot;

/// <summary>
/// Loot entry used by <see cref="LootTemplateDefinition" />.
/// </summary>
public class LootTemplateEntry
{
    /// <summary>
    /// Relative selection weight for this entry.
    /// </summary>
    public int Weight { get; set; } = 1;

    /// <summary>
    /// Item template id reference when using Moongate item templates.
    /// </summary>
    public string? ItemTemplateId { get; set; }

    /// <summary>
    /// Optional raw item id (for imported datasets that still use item ids).
    /// </summary>
    public string? ItemId { get; set; }

    /// <summary>
    /// Optional item template tag used to resolve a random matching item template.
    /// </summary>
    public string? ItemTag { get; set; }

    /// <summary>
    /// Fixed quantity produced by this entry.
    /// </summary>
    public int Amount { get; set; } = 1;

    /// <summary>
    /// Probability used by additive loot tables.
    /// </summary>
    public double Chance { get; set; } = 1.0;

    /// <summary>
    /// Minimum quantity produced by additive loot tables.
    /// </summary>
    public int? AmountMin { get; set; }

    /// <summary>
    /// Maximum quantity produced by additive loot tables.
    /// </summary>
    public int? AmountMax { get; set; }
}
