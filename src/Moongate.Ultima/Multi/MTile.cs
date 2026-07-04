using System;

using Moongate.Ultima.Graphics;

using Moongate.Ultima.Tiles;

namespace Moongate.Ultima.Multi;

public struct MTile : IComparable
{
    public ushort Id { get; internal set; }
    public sbyte Z { get; set; }

    public sbyte Flag { get; set; }

    public int Unk1 { get; set; }

    public int Solver { get; set; }

    public MTile(ushort id, sbyte z)
    {
        Id = Art.GetLegalItemId(id);
        Z = z;
        Flag = 1;
        Solver = 0;
        Unk1 = 0;
    }

    public MTile(ushort id, sbyte z, sbyte flag)
    {
        Id = Art.GetLegalItemId(id);
        Z = z;
        Flag = flag;
        Solver = 0;
        Unk1 = 0;
    }

    public MTile(ushort id, sbyte z, sbyte flag, int unk1)
    {
        Id = Art.GetLegalItemId(id);
        Z = z;
        Flag = flag;
        Solver = 0;
        Unk1 = unk1;
    }

    public void Set(ushort id, sbyte z)
    {
        Id = Art.GetLegalItemId(id);
        Z = z;
    }

    public void Set(ushort id, sbyte z, sbyte flag)
    {
        Id = Art.GetLegalItemId(id);
        Z = z;
        Flag = flag;
    }

    public void Set(ushort id, sbyte z, sbyte flag, int unk1)
    {
        Id = Art.GetLegalItemId(id);
        Z = z;
        Flag = flag;
        Unk1 = unk1;
    }

    public int CompareTo(object obj)
    {
        if (obj == null)
        {
            return 1;
        }

        if (!(obj is MTile))
        {
            throw new ArgumentNullException();
        }

        var a = (MTile)obj;

        ItemData ourData = TileData.ItemTable[Id];
        ItemData theirData = TileData.ItemTable[a.Id];

        int ourThreshold = 0;
        if (ourData.Height > 0)
        {
            ++ourThreshold;
        }

        if (!ourData.Background)
        {
            ++ourThreshold;
        }

        int ourZ = Z;
        int theirThreshold = 0;
        if (theirData.Height > 0)
        {
            ++theirThreshold;
        }

        if (!theirData.Background)
        {
            ++theirThreshold;
        }

        int theirZ = a.Z;

        ourZ += ourThreshold;
        theirZ += theirThreshold;
        int res = ourZ - theirZ;
        if (res == 0)
        {
            res = ourThreshold - theirThreshold;
        }

        if (res == 0)
        {
            res = Solver - a.Solver;
        }

        return res;
    }
}
