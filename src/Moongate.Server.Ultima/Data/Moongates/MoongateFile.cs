namespace Moongate.Server.Ultima.Data.Moongates;

/// <summary>
///     The root of <c>data/moongates.toml</c>: one <c>[[facet]]</c> per map.
/// </summary>
public class MoongateFile
{
    public List<MoongateFacet> Facet { get; set; } = [];
}
