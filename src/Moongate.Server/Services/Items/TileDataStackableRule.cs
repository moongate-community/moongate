using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Reads stackability off the client's own tiledata: <see cref="TileFlagType.Generic" /> on an item
/// tile is UO's "this is a pile" bit, so the shard agrees with what the client already draws — 898
/// tiles carry it, against the handful a hand-written table would ever cover.
/// <para>
/// The client files win: when the tables are loaded, the template's <c>Stackable</c> is not consulted
/// at all. It stays the fallback for the cases where they are not — an id the tiledata does not
/// describe, and tests, which run without a client directory.
/// </para>
/// </summary>
public sealed class TileDataStackableRule : IStackableRule
{
    public bool IsStackable(ItemEntity item, ItemTemplate? template)
    {
        if (TileData.ItemTable is { Length: > 0 } tiles && item.ItemId >= 0 && item.ItemId < tiles.Length)
        {
            return (tiles[item.ItemId].Flags & TileFlagType.Generic) != 0;
        }

        return template?.Stackable == true;
    }
}
