using Moongate.Server.Ultima.Data.Locations;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The named places staff travels to: those of <c>data/locations.toml</c> that lie inside a map the server loads,
///     as a tree of maps and categories. Worked out at the first call.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ILocationService
{
    /// <summary>
    ///     Gets one level of the tree. The empty path is the top, whose categories are the maps that have places; a
    ///     longer one names a map, then its categories, joined by <c>/</c> and read whatever their case, such as
    ///     <c>felucca/dungeons/covetous</c>.
    /// </summary>
    /// <returns>
    ///     Null when nothing has that path.
    /// </returns>
    LocationNode? GetNode(string path);

    /// <summary>
    ///     Finds the places <paramref name="text" /> names, whatever its case: a name, or the last words of the
    ///     categories and the name together, such as <c>entrance</c>, <c>covetous entrance</c> or
    ///     <c>dungeons covetous entrance</c>. When no place fits, a category named that way gives its first place.
    /// </summary>
    /// <returns>
    ///     The places of <paramref name="own" /> that fit; when none of that map fits, those of the other maps, in file
    ///     order. Empty when nothing fits.
    /// </returns>
    IReadOnlyList<NamedLocation> Find(string text, MapType own);
}
