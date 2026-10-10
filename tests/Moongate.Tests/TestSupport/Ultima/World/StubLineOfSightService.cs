using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Answers every line of sight with <see cref="Allow" /> (less what <see cref="Blocks" /> hides) and records what it was asked.
/// </summary>
public sealed class StubLineOfSightService : ILineOfSightService
{
    public bool Allow { get; set; } = true;

    /// <summary>
    ///     When set, a target for which it is true is out of sight, whatever <see cref="Allow" /> says.
    /// </summary>
    public Func<Point3D, bool>? Blocks { get; set; }

    public List<(Point3D From, Point3D To)> Checks { get; } = [];

    public bool HasLineOfSight(MapType map, Point3D origin, Point3D target)
    {
        Checks.Add((origin, target));

        return Allow && Blocks?.Invoke(target) != true;
    }
}
