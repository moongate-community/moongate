using Moongate.Core.Geometry;
using Moongate.Server.Abstractions.Interfaces.World;

namespace Moongate.Tests.Support;

/// <summary>Answers a fixed light level, so a test can drive the push without a clock or regions.</summary>
public sealed class StubLightService : ILightService
{
    private readonly int _level;

    public int? Override { get; set; }

    public StubLightService(int level)
    {
        _level = level;
    }

    public int LevelFor(int mapId, Point3D position)
        => Override ?? _level;
}
