namespace Moongate.Core.Types.Geometry;

/// <summary>
/// Eight compass directions encoded in the low three bits, with an optional running flag.
/// </summary>
[Flags]
public enum DirectionType : byte
{
    North = 0x0,
    NorthEast = 0x1,
    East = 0x2,
    SouthEast = 0x3,
    South = 0x4,
    SouthWest = 0x5,
    West = 0x6,
    NorthWest = 0x7,

    Running = 0x80
}
