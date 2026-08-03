using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Reads containerhood off the client's own tiledata, the way <see cref="TileDataStackableRule" />
/// reads stackability: <see cref="TileFlagType.Container" /> is the bit the client itself uses to
/// decide whether double-clicking a thing opens a gump.
/// <para>
/// The client files win. The template's <c>Category</c> is the fallback for an id the tables do not
/// describe, and for tests, which run without a client directory.
/// </para>
/// </summary>
public sealed class TileDataContainerRule : IContainerRule
{
    public bool IsContainer(ItemEntity item, ItemTemplate? template)
    {
        if (TileData.ItemTable is { Length: > 0 } tiles && item.ItemId >= 0 && item.ItemId < tiles.Length)
        {
            return (tiles[item.ItemId].Flags & TileFlagType.Container) != 0;
        }

        return string.Equals(template?.Category, "Container", StringComparison.OrdinalIgnoreCase);
    }
}
