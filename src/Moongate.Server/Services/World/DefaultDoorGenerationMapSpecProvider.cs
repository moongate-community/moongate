using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Services.World;

/// <summary>
/// Default hardcoded map specs aligned with ModernUO distribution regions used for door scanning.
/// </summary>
public sealed class DefaultDoorGenerationMapSpecProvider : IDoorGenerationMapSpecProvider
{
    private static readonly IReadOnlyList<DoorGenerationMapSpec> DefaultMapSpecs =
    [
        new(
            1,
            [
                new(new Point2D(250, 750), new Point2D(775, 1330)),
                new(new Point2D(525, 2095), new Point2D(925, 2430)),
                new(new Point2D(1025, 2155), new Point2D(1265, 2310)),
                new(new Point2D(1635, 2430), new Point2D(1705, 2508)),
                new(new Point2D(1775, 2605), new Point2D(2165, 2975)),
                new(new Point2D(1055, 3520), new Point2D(1570, 4075)),
                new(new Point2D(2860, 3310), new Point2D(3120, 3630)),
                new(new Point2D(2470, 1855), new Point2D(3950, 3045)),
                new(new Point2D(3425, 990), new Point2D(3900, 1455)),
                new(new Point2D(4175, 735), new Point2D(4840, 1600)),
                new(new Point2D(2375, 330), new Point2D(3100, 1045)),
                new(new Point2D(2100, 1090), new Point2D(2310, 1450)),
                new(new Point2D(1495, 1400), new Point2D(1550, 1475)),
                new(new Point2D(1085, 1520), new Point2D(1415, 1910)),
                new(new Point2D(1410, 1500), new Point2D(1745, 1795)),
                new(new Point2D(5120, 2300), new Point2D(6143, 4095))
            ]
        ),
        new(
            0,
            [
                new(new Point2D(250, 750), new Point2D(775, 1330)),
                new(new Point2D(525, 2095), new Point2D(925, 2430)),
                new(new Point2D(1025, 2155), new Point2D(1265, 2310)),
                new(new Point2D(1635, 2430), new Point2D(1705, 2508)),
                new(new Point2D(1775, 2605), new Point2D(2165, 2975)),
                new(new Point2D(1055, 3520), new Point2D(1570, 4075)),
                new(new Point2D(2860, 3310), new Point2D(3120, 3630)),
                new(new Point2D(2470, 1855), new Point2D(3950, 3045)),
                new(new Point2D(3425, 990), new Point2D(3900, 1455)),
                new(new Point2D(4175, 735), new Point2D(4840, 1600)),
                new(new Point2D(2375, 330), new Point2D(3100, 1045)),
                new(new Point2D(2100, 1090), new Point2D(2310, 1450)),
                new(new Point2D(1495, 1400), new Point2D(1550, 1475)),
                new(new Point2D(1085, 1520), new Point2D(1415, 1910)),
                new(new Point2D(1410, 1500), new Point2D(1745, 1795)),
                new(new Point2D(5120, 2300), new Point2D(6143, 4095))
            ]
        ),
        new(2, [new(new Point2D(0, 0), new Point2D(288 * 8, 200 * 8))]),
        new(3, [new(new Point2D(0, 0), new Point2D(320 * 8, 256 * 8))])
    ];

    /// <inheritdoc />
    public IReadOnlyList<DoorGenerationMapSpec> GetMapSpecs()
        => DefaultMapSpecs;
}
