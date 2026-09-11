using System.Runtime.InteropServices;

namespace Moongate.Ultima.Tiles;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct OldItemTileDataMul
{
    public readonly uint flags;
    public readonly byte weight;
    public readonly byte quality;
    public readonly short miscData;
    public readonly byte unk2;
    public readonly byte quantity;
    public readonly short anim;
    public readonly byte unk3;
    public readonly byte hue;
    public readonly byte stackingOffset;
    public readonly byte value;
    public readonly byte height;
    public fixed byte name[20];
}
