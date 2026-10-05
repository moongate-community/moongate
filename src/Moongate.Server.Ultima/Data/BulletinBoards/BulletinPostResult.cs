using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.BulletinBoards;

namespace Moongate.Server.Ultima.Data.BulletinBoards;

/// <summary>
///     What came of a post on a bulletin board: the message when it was posted, the messages the board let go to
///     make room for it, or how long the poster still waits.
/// </summary>
public sealed class BulletinPostResult
{
    public BulletinPostResultType Type { get; init; }

    /// <summary>
    ///     The message posted; null when nothing was.
    /// </summary>
    public BulletinMessageEntity? Message { get; init; }

    /// <summary>
    ///     The messages a full board removed to make room.
    /// </summary>
    public IReadOnlyList<Serial> Dropped { get; init; } = [];

    /// <summary>
    ///     The seconds the poster still waits, on <see cref="BulletinPostResultType.TooSoon" />.
    /// </summary>
    public int WaitSeconds { get; init; }
}
