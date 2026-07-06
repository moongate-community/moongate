using Moongate.Ultima.Interfaces;
using System.Runtime.InteropServices;
using Moongate.Ultima.Types;

namespace Moongate.Ultima.Io;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Entry6D : IEntry
{
    public IEntry Invalid => new Entry6D();

    public int Lookup { get; set; }

    public int Length { get; set; }

    private int _extra1;
    private int _extra2;

    public int Extra
    {
        get => (_extra1 << 16) | _extra2;
        set
        {
            _extra1 = value & 0x0000FFFF;
            _extra2 = (int)((value & 0xFFFF0000) >> 16);
        }
    }

    public int DecompressedLength { get; set; }

    public int Extra1 { get; set; }

    public int Extra2 { get; set; }

    public CompressionFlagType Flag { get; set; }
}
