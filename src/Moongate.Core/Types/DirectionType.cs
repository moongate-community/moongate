using System;

namespace Moongate.Core.Types;

[Flags]

/// <summary>
/// Represents DirectionType.
/// </summary>
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
