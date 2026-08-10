namespace Moongate.UO.Data.Types;

/// <summary>
/// Which way a door hangs and which way it swings. The order is UO's own: a door style occupies a
/// block of sixteen graphics, and the closed graphic for a facing is <c>base + 2 × facing</c>, so
/// these values are indices into the art rather than names anyone chose.
/// </summary>
public enum DoorFacingType
{
    WestCW = 0,
    EastCCW = 1,
    WestCCW = 2,
    EastCW = 3,
    SouthCW = 4,
    NorthCCW = 5,
    SouthCCW = 6,
    NorthCW = 7
}
