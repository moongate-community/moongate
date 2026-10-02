using Moongate.Server.Ultima.Data.Moongates;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The public moongates the world really has: those of <c>data/moongates.toml</c> on the maps the server loads.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IPublicMoongateService
{
    /// <summary>
    ///     Gets the facets of the loaded maps, in file order, each with the destinations that lie inside its map; the
    ///     height of a destination marked <c>average_z</c> is the map's at that spot. A facet with none left is not
    ///     listed. Worked out at the first call.
    /// </summary>
    IReadOnlyList<MoongateFacet> GetFacets();
}
