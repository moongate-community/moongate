namespace Moongate.Core.Types;

/// <summary>
/// UO movement direction as sent on the wire. The low 3 bits encode the facing;
/// bit 7 (<see cref="Running"/>) marks a running step.
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
