using System.Runtime.InteropServices;

namespace Moongate.Ultima;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct OldLandTileDataMul
{
    public readonly uint flags;
    public readonly ushort texID;
    public fixed byte name[20];
}
