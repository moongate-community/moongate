using System.Runtime.InteropServices;

namespace Moongate.Ultima;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct StaticTile
{
    public ushort Id;
    public byte X;
    public byte Y;
    public sbyte Z;
    public short Hue;
}
