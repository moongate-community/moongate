using System.Collections.Generic;

namespace Moongate.Ultima.Maps;

public sealed class HuedTileList
{
    private readonly List<HuedTile> _tiles;
    public HuedTileList()
    {
        _tiles = new List<HuedTile>();
    }

    public int Count { get { return _tiles.Count; } }

    public void Add(ushort id, short hue, sbyte z)
    {
        _tiles.Add(new HuedTile(id, hue, z));
    }

    public HuedTile[] ToArray()
    {
        var tiles = new HuedTile[Count];

        if (_tiles.Count > 0)
        {
            _tiles.CopyTo(tiles);
        }

        _tiles.Clear();

        return tiles;
    }
}
