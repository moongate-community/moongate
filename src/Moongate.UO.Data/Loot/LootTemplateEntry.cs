namespace Moongate.UO.Data.Loot;

public sealed class LootTemplateEntry
{
    public int? Weight { get; set; }
    public string? ItemTemplateId { get; set; }
    public string? ItemTag { get; set; }
    public int? Amount { get; set; }
    public double? Chance { get; set; }
    public int? AmountMin { get; set; }
    public int? AmountMax { get; set; }
}
