using System.Runtime.InteropServices;
using Moongate.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Ultima.Io;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Entry3D : IEntry
{
    // The three auto-properties below define the 12-byte on-disk index record; their
    // declaration order fixes the backing-field layout read via MemoryMarshal.
    public int Lookup { get; set; }

    public int Length { get; set; }

    public int Extra { get; set; }

    public int DecompressedLength
    {
        get => Length;
        set => Length = value;
    }

    public int Extra1
    {
        get => (int)((Extra & 0xFFFF0000) >> 16);
        set => Extra = (Extra & 0x0000FFFF) | (value << 16);
    }

    public int Extra2
    {
        get => Extra & 0x0000FFFF;
        set => Extra = (int)((Extra & 0xFFFF0000) | (uint)value);
    }

    public CompressionFlagType Flag
    {
        get => CompressionFlagType.None;
        set { }
    } // No compression, means that we have only three first fields
}
