namespace Moongate.UO.Data.Regions;

/// <summary>A reference to a region's parent region (by name + map).</summary>
public sealed class RegionParent
{
    public string Name { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
}
