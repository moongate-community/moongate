using System.Runtime.InteropServices;

using Moongate.Ultima.Types;

namespace Moongate.Ultima;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Entry3D : IEntry
{
    // do not mess with the fields struct layout in memory is important because of how we read the index files.
    private int lookup;
    private int length;
    private int extra;

#pragma warning disable S2292
    public int Lookup { get => lookup; set => lookup = value; }

    public int Length { get => length; set => length = value; }

    public int Extra { get => extra; set => extra = value; }

    public int DecompressedLength { get => length; set => length = value; }
#pragma warning restore S2292

    public int Extra1
    {
        get => (int)((Extra & 0xFFFF0000) >> 16);
        set => Extra = Extra & 0x0000FFFF | (value << 16);
    }

    public int Extra2
    {
        get => Extra & 0x0000FFFF;
        set => Extra = (int)((Extra & 0xFFFF0000) | (uint)value);
    }

    public CompressionFlag Flag { get => CompressionFlag.None; set { } } // No compression, means that we have only three first fields
}
