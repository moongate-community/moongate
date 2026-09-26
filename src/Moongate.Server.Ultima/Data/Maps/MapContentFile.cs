namespace Moongate.Server.Ultima.Data.Maps;

/// <summary>
///     The root of <c>maps.toml</c>: an array of tables under <c>map</c>. A TOML document is always a table, so
///     it cannot be read straight into an array.
/// </summary>
internal sealed class MapContentFile
{
    public List<MapContent> Map { get; set; } = [];
}
