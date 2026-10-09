using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What is gathered from the world and runs out by area: the resources of <c>data/harvest.toml</c>, such as the
///     fish of the sea. Each map is cut in areas of the resource's size; an area is drawn full the first time it is
///     asked for, and is full again, all at once, some time after the first take from it. Kept in memory: a restart
///     fills every area. Game loop only.
/// </summary>
public interface IHarvestService
{
    /// <summary>
    ///     Gets whether <c>data/harvest.toml</c> has a resource of that id.
    /// </summary>
    bool Has(string resource);

    /// <summary>
    ///     Gets how much of <paramref name="resource" /> is left in the area of a cell.
    /// </summary>
    /// <returns>
    ///     Null for an unknown resource or a cell below zero.
    /// </returns>
    int? Amount(string resource, MapType map, int x, int y);

    /// <summary>
    ///     Takes one of <paramref name="resource" /> from the area of a cell.
    /// </summary>
    /// <returns>
    ///     False when the area has none left, for an unknown resource or a cell below zero.
    /// </returns>
    bool TryTake(string resource, MapType map, int x, int y);
}
