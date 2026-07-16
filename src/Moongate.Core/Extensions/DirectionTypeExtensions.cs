using Moongate.Core.Types;

namespace Moongate.Core.Extensions;

/// <summary>
/// Movement helpers for <see cref="DirectionType" />.
/// </summary>
public static class DirectionTypeExtensions
{
    private const byte FacingMask = 0x7;

    /// <summary>True when the running flag is set.</summary>
    public static bool IsRunning(this DirectionType direction)
        => (direction & DirectionType.Running) != 0;

    /// <summary>Returns the reverse facing, preserving the running flag.</summary>
    public static DirectionType Opposite(this DirectionType direction)
    {
        var running = (byte)direction & (byte)DirectionType.Running;
        var facing = (((byte)direction & FacingMask) + 4) & FacingMask;

        return (DirectionType)(facing | running);
    }

    /// <summary>Returns the facing without the running flag.</summary>
    public static DirectionType StripRunning(this DirectionType direction)
        => (DirectionType)((byte)direction & FacingMask);

    /// <summary>
    /// Returns the map delta of one step in this direction, in UO screen
    /// coordinates (north decreases Y). The running flag is ignored.
    /// </summary>
    public static (int X, int Y) ToOffset(this DirectionType direction)
        => (DirectionType)((byte)direction & FacingMask) switch
        {
            DirectionType.North     => (0, -1),
            DirectionType.NorthEast => (1, -1),
            DirectionType.East      => (1, 0),
            DirectionType.SouthEast => (1, 1),
            DirectionType.South     => (0, 1),
            DirectionType.SouthWest => (-1, 1),
            DirectionType.West      => (-1, 0),
            _                       => (-1, -1)
        };
}
