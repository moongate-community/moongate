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
    ///     Finds the places <paramref name="text" /> names, whatever its case. First what is named exactly so: a place
    ///     by its name or by the last of its categories and its name, such as <c>entrance</c>,
    ///     <c>covetous entrance</c> or <c>dungeons covetous entrance</c>; else a category, which gives its first place.
    ///     When nothing is named so, the places whose name ends with those words, such as <c>haven</c> for
    ///     <c>Old Haven</c>.
    /// </summary>
    /// <returns>
    ///     What fits on <paramref name="own" />, a place before a category; when nothing of that map fits, what fits on
    ///     the other maps, in file order. Empty when nothing fits.
    /// </returns>
    IReadOnlyList<NamedLocation> Find(string text, MapType own);
}
