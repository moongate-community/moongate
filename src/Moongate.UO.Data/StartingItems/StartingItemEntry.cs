namespace Moongate.UO.Data.StartingItems;

/// <summary>One item in a starting kit: a template id, a stack amount and an optional hue token.</summary>
public sealed class StartingItemEntry
{
    public string Item { get; set; } = string.Empty;
    public int Amount { get; set; } = 1;
    public string? Hue { get; set; }
    public bool Newbie { get; set; }
}
