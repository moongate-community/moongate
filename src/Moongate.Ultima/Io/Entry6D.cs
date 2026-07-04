using System.Runtime.InteropServices;
using Moongate.Ultima.Types;

namespace Moongate.Ultima.Io;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Entry6D : IEntry
{
    public IEntry Invalid => new Entry6D();

    public int Lookup { get; set; }

    public int Length { get; set; }

    private int extra1;
    private int extra2;

    public int Extra
    {
        get => (extra1 << 16) | extra2;
        set
        {
            extra1 = value & 0x0000FFFF;
            extra2 = (int)((value & 0xFFFF0000) >> 16);
        }
    }

    public int DecompressedLength { get; set; }

    public int Extra1 { get; set; }

    public int Extra2 { get; set; }

    public CompressionFlag Flag { get; set; }
}
