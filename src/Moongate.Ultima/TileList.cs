using System.Collections.Generic;

namespace Moongate.Ultima;

public sealed class TileList
{
    private readonly List<Tile> _tiles;

    public TileList()
    {
        _tiles = new List<Tile>();
    }

    public int Count { get { return _tiles.Count; } }

    public void Add(ushort id, sbyte z)
    {
        _tiles.Add(new Tile(id, z));
    }

    public Tile[] ToArray()
    {
        var tiles = new Tile[Count];
        if (_tiles.Count > 0)
        {
            _tiles.CopyTo(tiles);
        }

        _tiles.Clear();

        return tiles;
    }

    public Tile Get(int i)
    {
        return _tiles[i];
    }
}
