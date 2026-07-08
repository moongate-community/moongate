namespace Moongate.UO.Data.Regions;

/// <summary>
/// A named map region: its kind (<see cref="Type" />), the rectangles it covers, and optional
/// metadata (music, parent, rune, expansion bounds, entrance/go points).
/// </summary>
public sealed class RegionDefinition
{
    public string Type { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public List<RegionRectangle> Area { get; set; } = [];
    public string? Music { get; set; }
    public RegionParent? Parent { get; set; }
    public string? RuneName { get; set; }
    public string? Rune { get; set; }
    public string? MinExpansion { get; set; }
    public string? MaxExpansion { get; set; }
    public RegionPoint? GoLocation { get; set; }
    public RegionPoint? Entrance { get; set; }
}
