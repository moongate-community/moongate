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
    ///     Gets or sets whether a search ran for <see cref="Goal" />.
    /// </summary>
    public bool Searched { get; set; }

    /// <summary>
    ///     Gets or sets when the last search ran, in milliseconds of the server clock.
    /// </summary>
    public long SearchedAt { get; set; }

    /// <summary>
    ///     Gets or sets whether the last search did not reach its goal: it found nothing, or only a way to somewhere
    ///     near.
    /// </summary>
    public bool Unreached { get; set; }

    /// <summary>
    ///     Gets or sets whether the step last given was one straight towards the goal, not one of <see cref="Steps" />.
    /// </summary>
    public bool Straight { get; set; }

    /// <summary>
    ///     Gets or sets whether the last step was refused: no straight step is given until a search ran.
    /// </summary>
    public bool StraightRefused { get; set; }

    public long LastUsedAt { get; set; }
}
