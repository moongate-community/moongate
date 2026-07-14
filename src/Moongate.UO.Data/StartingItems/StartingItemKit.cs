namespace Moongate.UO.Data.StartingItems;

/// <summary>A starting kit split into worn (Equip) and backpack (Pack) entries.</summary>
public sealed class StartingItemKit
{
    public List<StartingItemEntry> Equip { get; set; } = [];
    public List<StartingItemEntry> Pack { get; set; } = [];
}
