using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.World;

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
                new(new(250, 750), new(775, 1330)),
                new(new(525, 2095), new(925, 2430)),
                new(new(1025, 2155), new(1265, 2310)),
                new(new(1635, 2430), new(1705, 2508)),
                new(new(1775, 2605), new(2165, 2975)),
                new(new(1055, 3520), new(1570, 4075)),
                new(new(2860, 3310), new(3120, 3630)),
                new(new(2470, 1855), new(3950, 3045)),
                new(new(3425, 990), new(3900, 1455)),
                new(new(4175, 735), new(4840, 1600)),
                new(new(2375, 330), new(3100, 1045)),
                new(new(2100, 1090), new(2310, 1450)),
                new(new(1495, 1400), new(1550, 1475)),
                new(new(1085, 1520), new(1415, 1910)),
                new(new(1410, 1500), new(1745, 1795)),
                new(new(5120, 2300), new(6143, 4095))
            ]
        ),
        new(
            0,
            [
                new(new(250, 750), new(775, 1330)),
                new(new(525, 2095), new(925, 2430)),
                new(new(1025, 2155), new(1265, 2310)),
                new(new(1635, 2430), new(1705, 2508)),
                new(new(1775, 2605), new(2165, 2975)),
                new(new(1055, 3520), new(1570, 4075)),
                new(new(2860, 3310), new(3120, 3630)),
                new(new(2470, 1855), new(3950, 3045)),
                new(new(3425, 990), new(3900, 1455)),
                new(new(4175, 735), new(4840, 1600)),
                new(new(2375, 330), new(3100, 1045)),
                new(new(2100, 1090), new(2310, 1450)),
                new(new(1495, 1400), new(1550, 1475)),
                new(new(1085, 1520), new(1415, 1910)),
                new(new(1410, 1500), new(1745, 1795)),
                new(new(5120, 2300), new(6143, 4095))
            ]
        ),
        new(2, [new(new(0, 0), new(288 * 8, 200 * 8))]),
        new(3, [new(new(0, 0), new(320 * 8, 256 * 8))])
    ];

    /// <inheritdoc />
    public IReadOnlyList<DoorGenerationMapSpec> GetMapSpecs()
        => DefaultMapSpecs;
}
