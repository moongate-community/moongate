using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Reads the terrain and the static objects of the maps listed in <c>data/maps.toml</c> from the client files.
/// </summary>
/// <remarks>
///     Starting opens, for each map, <c>map{n}.mul</c> or <c>map{n}LegacyMUL.uop</c>, <c>staidx{n}.mul</c> and
///     <c>statics{n}.mul</c>, where <c>n</c> is the map's <c>file_index</c>, and fails with
///     <see cref="FileNotFoundException" /> when one is missing. Blocks of 8x8 cells are read the first time a cell in
///     them is asked for and kept in a bounded cache. Call it from the game loop: reads share one file handle per map.
/// </remarks>
public interface IMapService : IMoongateStartupService
{
    /// <summary>
    ///     Gets the maps that were loaded, in the order of <c>data/maps.toml</c>.
    /// </summary>
    IReadOnlyList<MapType> Maps { get; }

    /// <summary>
    ///     Gets whether <paramref name="map" /> was loaded and <paramref name="x" />, <paramref name="y" /> lies inside it.
    /// </summary>
    bool Contains(MapType map, int x, int y);

    /// <summary>
    ///     Gets the terrain of cell <paramref name="x" />, <paramref name="y" /> of <paramref name="map" />.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     <paramref name="map" /> was not loaded.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     The cell lies outside the map.
    /// </exception>
    MapLandTile GetLand(MapType map, int x, int y);

    /// <summary>
    ///     Gets the static objects of cell <paramref name="x" />, <paramref name="y" /> of <paramref name="map" />, in
    ///     file order; empty when the cell has none.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     <paramref name="map" /> was not loaded.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     The cell lies outside the map.
    /// </exception>
    IReadOnlyList<MapStaticTile> GetStatics(MapType map, int x, int y);
}
