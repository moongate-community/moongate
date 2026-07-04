using System.Runtime.InteropServices;

namespace Moongate.Ultima.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct HueDataMul
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public readonly ushort[] colors;

    public readonly ushort tableStart;
    public readonly ushort tableEnd;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public readonly byte[] name;
}
