using System.Runtime.InteropServices;

namespace Moongate.Ultima.Tiles;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NewLandTileDataMul
{
    public readonly ulong flags;
    public readonly ushort texID;
    public fixed byte name[20];
}
