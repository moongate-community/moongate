namespace Moongate.Server.Ultima.Types.Decorations;

/// <summary>
///     Which way a door hangs and swings, as ModernUO's <c>DoorFacing</c>, in the same order: a door's closed graphic is
///     its kind's first graphic plus twice this value, and the open one follows it. Written in TOML as
///     <c>west_cw</c>, <c>east_ccw</c> and so on.
/// </summary>
public enum DoorFacingType
{
    /// <summary>West side, swinging clockwise.</summary>
    WestCW = 0,

    /// <summary>East side, swinging counterclockwise.</summary>
    EastCCW = 1,

    /// <summary>West side, swinging counterclockwise.</summary>
    WestCCW = 2,

    /// <summary>East side, swinging clockwise.</summary>
    EastCW = 3,

    /// <summary>South side, swinging clockwise.</summary>
    SouthCW = 4,

    /// <summary>North side, swinging counterclockwise.</summary>
    NorthCCW = 5,

    /// <summary>South side, swinging counterclockwise.</summary>
    SouthCCW = 6,

    /// <summary>North side, swinging clockwise.</summary>
    NorthCW = 7,

    /// <summary>A sliding door on the south side, sliding west.</summary>
    SouthSW = 8,

    /// <summary>A sliding door on the south side, sliding east.</summary>
    SouthSE = 9,

    /// <summary>A sliding door on the west side, sliding south.</summary>
    WestSS = 10,

    /// <summary>A sliding door on the west side, sliding north.</summary>
    WestSN = 11
}
