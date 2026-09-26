using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Tiles;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Gives the land and item graphics of <c>tiledata.mul</c>: names, flags, weights and heights, which movement,
///     line of sight and item properties read.
/// </summary>
/// <remarks>
///     The first call copies the tables that <c>IUltimaDataService</c> loaded at startup into read-only arrays, so every
///     later lookup is an array index and callers on any thread see the same tiles. Calling before the tables are
///     loaded throws <see cref="InvalidOperationException" />.
/// </remarks>
public interface ITileDataService
{
    /// <summary>
    ///     Gets the number of land graphics, the highest land id plus one.
    /// </summary>
    int LandCount { get; }

    /// <summary>
    ///     Gets the number of item graphics, the highest item id plus one; it depends on the client version.
    /// </summary>
    int ItemCount { get; }

    /// <summary>
    ///     Gets land graphic <paramref name="id" />.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="id" /> is negative or not below <see cref="LandCount" />.
    /// </exception>
    LandTile GetLand(int id);

    /// <summary>
    ///     Gets item graphic <paramref name="id" />.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="id" /> is negative or not below <see cref="ItemCount" />.
    /// </exception>
    ItemTile GetItem(int id);

    /// <summary>
    ///     Gets land graphic <paramref name="id" />, or false when the id is out of range.
    /// </summary>
    bool TryGetLand(int id, [NotNullWhen(true)] out LandTile? tile);

    /// <summary>
    ///     Gets item graphic <paramref name="id" />, or false when the id is out of range.
    /// </summary>
    bool TryGetItem(int id, [NotNullWhen(true)] out ItemTile? tile);
}
