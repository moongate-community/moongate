using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Internal.Movement;

/// <summary>
///     The path an NPC is walking: where to, the steps left, where it should stand before the next one, and when a new
///     search may run.
/// </summary>
public sealed class NpcPath
{
    public MapType Map { get; set; }

    public Point3D Goal { get; set; }

    public Queue<DirectionType> Steps { get; } = new();

    /// <summary>
    ///     Gets or sets where the NPC stands when the path is still good; elsewhere, something else moved it.
    /// </summary>
    public Point3D Expected { get; set; }

    /// <summary>
    ///     Gets or sets the first moment a new search may run, in milliseconds of the server clock.
    /// </summary>
    public long NextSearchAt { get; set; }

    /// <summary>
    ///     Gets or sets whether the last search found no way.
    /// </summary>
    public bool Failed { get; set; }

    public long LastUsedAt { get; set; }
}
