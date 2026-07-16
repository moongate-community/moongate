using System.Runtime.InteropServices;

namespace Moongate.Ultima.Maps;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct StaticTile
{
    // The auto-properties define the 7-byte on-disk statics record; their declaration
    // order fixes the backing-field layout read via MemoryMarshal.
    public ushort Id { get; set; }

    public byte X { get; set; }

    public byte Y { get; set; }

    public sbyte Z { get; set; }

    public short Hue { get; set; }
}
