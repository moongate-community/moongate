using System.Runtime.InteropServices;

namespace Moongate.Ultima.Maps;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HuedTile
{
    public ushort Id { get; set; }

    public int Hue { get; set; }

    public sbyte Z { get; set; }

    public HuedTile(ushort id, short hue, sbyte z)
    {
        Id = id;
        Hue = hue;
        Z = z;
    }
}
