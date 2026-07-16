namespace Moongate.UO.Data.StartingItems;

/// <summary>The whole starting-items table: a universal kit, per-body kits and per-skill kits.</summary>
public sealed class StartingItemsData
{
    public StartingItemKit All { get; set; } = new();
    public Dictionary<string, StartingItemKit> ByBody { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, StartingItemKit> BySkill { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
