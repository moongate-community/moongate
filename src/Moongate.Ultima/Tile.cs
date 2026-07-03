using System;
using System.Runtime.InteropServices;

namespace Moongate.Ultima;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Tile : IComparable
{
    public ushort Id { get; internal set; }

    public sbyte Z { get; set; }

    public Tile(ushort id, sbyte z)
    {
        Id = id;
        Z = z;
    }

    public void Set(ushort id, sbyte z)
    {
        Id = id;
        Z = z;
    }

    public int CompareTo(object obj)
    {
        if (obj == null)
        {
            return 1;
        }

        if (!(obj is Tile))
        {
            throw new ArgumentNullException();
        }

        var a = (Tile)obj;

        if (Z > a.Z)
        {
            return 1;
        }

        if (a.Z > Z)
        {
            return -1;
        }

        ItemData ourData = TileData.ItemTable[Id];
        ItemData theirData = TileData.ItemTable[a.Id];

        if (ourData.Height > theirData.Height)
        {
            return 1;
        }

        if (theirData.Height > ourData.Height)
        {
            return -1;
        }

        if (ourData.Background && !theirData.Background)
        {
            return -1;
        }

        if (theirData.Background && !ourData.Background)
        {
            return 1;
        }

        return 0;
    }
}
