using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Movement;

/// <summary>
///     Records the teleports asked for and answers them with <see cref="Result" />, moving nobody.
/// </summary>
public sealed class RecordingTeleportService : ITeleportService
{
    public List<(MobileEntity Mobile, Point3D Location)> Teleports { get; } = [];

    public bool Result { get; set; } = true;

    public bool Teleport(MobileEntity mobile, Point3D location)
    {
        Teleports.Add((mobile, location));

        return Result;
    }
}
