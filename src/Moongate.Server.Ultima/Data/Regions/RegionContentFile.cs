namespace Moongate.Server.Ultima.Data.Regions;

/// <summary>
///     The root of a region file: an array of tables under <c>region</c>. The property name must match the table
///     name, otherwise the file reads as an empty list without any error.
/// </summary>
internal sealed class RegionContentFile
{
    public List<RegionContent> Region { get; set; } = [];
}
