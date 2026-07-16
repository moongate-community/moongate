namespace Moongate.UO.Data.Items;

/// <summary>Container attributes: capacity, gump, default contents and (for quivers) ammo bonuses.</summary>
public sealed class ContainerSpec
{
    public int? WeightMax { get; set; }
    public int? MaxItems { get; set; }
    public int? GumpId { get; set; }
    public string? ContainerLayoutId { get; set; }
    public List<string>? Contents { get; set; }
    public bool? IsQuiver { get; set; }
    public int? WeightReduction { get; set; }
    public int? QuiverDamageIncrease { get; set; }
    public int? LowerAmmoCost { get; set; }
    public int? DefenseChanceIncrease { get; set; }
}
