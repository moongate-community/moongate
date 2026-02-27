using System.Runtime.InteropServices;

namespace Moongate.UO.Data.Files;

[StructLayout(LayoutKind.Sequential, Pack = 1)]

/// <summary>
/// Represents Entry3D.
/// </summary>
public struct Entry3D
{
    public int lookup;
    public int length;
    public int extra;
}
