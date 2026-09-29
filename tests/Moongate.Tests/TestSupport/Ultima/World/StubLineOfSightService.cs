using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Answers every line of sight with <see cref="Allow" /> and records what it was asked.
/// </summary>
public sealed class StubLineOfSightService : ILineOfSightService
{
    public bool Allow { get; set; } = true;

    public List<(Point3D From, Point3D To)> Checks { get; } = [];

    public bool HasLineOfSight(MapType map, Point3D origin, Point3D target)
    {
        Checks.Add((origin, target));

        return Allow;
    }
}
