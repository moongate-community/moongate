using Moongate.Server.Ultima.Data.Tiles;

namespace Moongate.Server.Ultima.Data.Internal.Movement;

/// <summary>
///     Something on a cell the movement checks: a static of the map or an item lying on the ground, with its tile data
///     and height. <see cref="Fixed" /> is false for an item that can be picked up, which nobody stands on.
/// </summary>
public readonly record struct CellTile(ItemTile Tile, int Z, bool IsItem, bool Fixed);
