using System.Collections.Generic;

namespace Moongate.Ultima.Multi;

public sealed class MTileList
{
    private readonly List<MTile> _tiles;

    public MTileList()
    {
        _tiles = new List<MTile>();
    }

    public int Count { get { return _tiles.Count; } }

    public void Add(ushort id, sbyte z)
    {
        _tiles.Add(new MTile(id, z));
    }

    public void Add(ushort id, sbyte z, sbyte flag)
    {
        _tiles.Add(new MTile(id, z, flag));
    }

    public void Add(ushort id, sbyte z, sbyte flag, int unk1)
    {
        _tiles.Add(new MTile(id, z, flag, unk1));
    }

    public MTile[] ToArray()
    {
        var tiles = new MTile[Count];

        if (_tiles.Count > 0)
        {
            _tiles.CopyTo(tiles);
        }

        _tiles.Clear();

        return tiles;
    }

    public MTile Get(int i)
    {
        return _tiles[i];
    }

    public void Set(int i, ushort id, sbyte z)
    {
        if (i < Count)
        {
            _tiles[i].Set(id, z);
        }
    }

    public void Set(int i, ushort id, sbyte z, sbyte flag)
    {
        if (i < Count)
        {
            _tiles[i].Set(id, z, flag);
        }
    }

    public void Set(int i, ushort id, sbyte z, sbyte flag, int unk1)
    {
        if (i < Count)
        {
            _tiles[i].Set(id, z, flag, unk1);
        }
    }
    public void Remove(int i)
    {
        if (i < Count)
        {
            _tiles.RemoveAt(i);
        }
    }
}
