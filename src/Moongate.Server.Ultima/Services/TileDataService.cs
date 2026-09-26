using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Tiles;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Tiles;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Copies the <see cref="TileData" /> tables into read-only arrays the first time a tile is asked for, so the service
///     can be created before <c>IUltimaDataService</c> has loaded <c>tiledata.mul</c>.
/// </summary>
public class TileDataService : ITileDataService
{
    private readonly Lazy<LandTile[]> _land;
    private readonly Lazy<ItemTile[]> _items;

    public int LandCount => _land.Value.Length;

    public int ItemCount => _items.Value.Length;

    public TileDataService()
        : this(() => TileData.LandTable, () => TileData.ItemTable)
    {
    }

    internal TileDataService(Func<LandData[]?> landTable, Func<ItemData[]?> itemTable)
    {
        _land = new(() => ToLandTiles(landTable() ?? throw NotLoaded()));
        _items = new(() => ToItemTiles(itemTable() ?? throw NotLoaded()));
    }

    public LandTile GetLand(int id)
    {
        return TryGetLand(id, out var tile)
                   ? tile
                   : throw new ArgumentOutOfRangeException(nameof(id), id, $"Land ids go from 0 to {LandCount - 1}.");
    }

    public ItemTile GetItem(int id)
    {
        return TryGetItem(id, out var tile)
                   ? tile
                   : throw new ArgumentOutOfRangeException(nameof(id), id, $"Item ids go from 0 to {ItemCount - 1}.");
    }

    public bool TryGetLand(int id, [NotNullWhen(true)] out LandTile? tile)
    {
        var land = _land.Value;
        tile = (uint)id < (uint)land.Length ? land[id] : null;

        return tile is not null;
    }

    public bool TryGetItem(int id, [NotNullWhen(true)] out ItemTile? tile)
    {
        var items = _items.Value;
        tile = (uint)id < (uint)items.Length ? items[id] : null;

        return tile is not null;
    }

    private static InvalidOperationException NotLoaded()
    {
        return new("tiledata.mul is not loaded: IUltimaDataService loads it when the server starts.");
    }

    private static LandTile[] ToLandTiles(LandData[] table)
    {
        var tiles = new LandTile[table.Length];

        for (var id = 0; id < table.Length; id++)
        {
            var land = table[id];
            tiles[id] = new()
            {
                Id = id,
                Name = land.Name ?? string.Empty,
                Flags = land.Flags,
                TextureId = land.TextureId
            };
        }

        return tiles;
    }

    private static ItemTile[] ToItemTiles(ItemData[] table)
    {
        var tiles = new ItemTile[table.Length];

        for (var id = 0; id < table.Length; id++)
        {
            var item = table[id];
            tiles[id] = new()
            {
                Id = id,
                Name = item.Name ?? string.Empty,
                Flags = item.Flags,
                Weight = item.Weight,
                Height = item.Height,
                StandHeight = item.CalcHeight,
                Layer = item.Quality,
                Quantity = item.Quantity,
                Animation = item.Animation
            };
        }

        return tiles;
    }
}
