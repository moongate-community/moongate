namespace Moongate.Ultima.Maps;

public sealed class TileList
{
    private readonly List<Tile> _tiles;

    public int Count => _tiles.Count;

    public TileList()
    {
        _tiles = new();
    }

    public void Add(ushort id, sbyte z)
        => _tiles.Add(new(id, z));

    public Tile Get(int i)
        => _tiles[i];

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
}
