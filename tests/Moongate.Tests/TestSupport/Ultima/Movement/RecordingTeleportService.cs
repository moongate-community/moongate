using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Movement;

/// <summary>
///     Records the teleports asked for and answers them with <see cref="Result" />, moving nobody.
/// </summary>
public sealed class RecordingTeleportService : ITeleportService
{
    public List<(MobileEntity Mobile, MapType Map, Point3D Location)> Teleports { get; } = [];

    public bool Result { get; set; } = true;

    /// <summary>
    ///     The maps a teleport to is refused, as maps that are not loaded.
    /// </summary>
    public HashSet<MapType> RefusedMaps { get; } = [];

    /// <summary>
    ///     The mobile whose teleport throws.
    /// </summary>
    public MobileEntity? ThrowFor { get; set; }

    /// <summary>
    ///     Gets the thread the last teleport was asked on.
    /// </summary>
    public int TeleportedOnThread { get; private set; }

    public bool Teleport(MobileEntity mobile, MapType map, Point3D location)
    {
        Teleports.Add((mobile, map, location));
        TeleportedOnThread = System.Environment.CurrentManagedThreadId;

        if (ReferenceEquals(mobile, ThrowFor))
        {
            throw new InvalidOperationException("The teleport failed.");
        }

        return Result && !RefusedMaps.Contains(map);
    }
}
