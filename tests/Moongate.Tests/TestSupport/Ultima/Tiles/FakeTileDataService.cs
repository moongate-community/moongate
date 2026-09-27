using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Tiles;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Tiles;

/// <summary>
///     In-memory tile data: 0x4000 land tiles and 0x10000 item tiles, all without flags unless set.
/// </summary>
public sealed class FakeTileDataService : ITileDataService
{
    private readonly LandTile[] _land = new LandTile[0x4000];
    private readonly ItemTile[] _items = new ItemTile[0x10000];

    public int LandCount => _land.Length;

    public int ItemCount => _items.Length;

    public FakeTileDataService()
    {
        for (var id = 0; id < _land.Length; id++)
        {
            _land[id] = new() { Id = id };
        }

        for (var id = 0; id < _items.Length; id++)
        {
            _items[id] = new() { Id = id };
        }
    }

    public FakeTileDataService Land(int id, TileFlagType flags)
    {
        _land[id] = new() { Id = id, Flags = flags };

        return this;
    }

    public FakeTileDataService Item(int id, TileFlagType flags, byte height, byte weight = 0, byte layer = 0)
    {
        var standHeight = (flags & TileFlagType.Bridge) != 0 ? height / 2 : height;
        _items[id] = new()
        {
            Id = id, Flags = flags, Height = height, StandHeight = standHeight, Weight = weight, Layer = layer
        };

        return this;
    }

    public LandTile GetLand(int id)
    {
        return _land[id];
    }

    public ItemTile GetItem(int id)
    {
        return _items[id];
    }

    public bool TryGetLand(int id, [NotNullWhen(true)] out LandTile? tile)
    {
        tile = (uint)id < (uint)_land.Length ? _land[id] : null;

        return tile is not null;
    }

    public bool TryGetItem(int id, [NotNullWhen(true)] out ItemTile? tile)
    {
        tile = (uint)id < (uint)_items.Length ? _items[id] : null;

        return tile is not null;
    }
}
