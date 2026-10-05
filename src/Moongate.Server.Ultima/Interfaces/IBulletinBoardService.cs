using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.BulletinBoards;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The messages of the bulletin boards: each board has its own, in threads of a first message and its replies.
///     Threads go some days after their last reply, and a board holds only so many messages.
/// </summary>
/// <remarks>
///     Game loop only, once started. The world save writes <see cref="Messages" /> and deletes the removed ones.
/// </remarks>
public interface IBulletinBoardService : IMoongateStartupService, IPersistenceDeletionSource
{
    /// <summary>
    ///     Gets the live messages of every board, which the world save writes.
    /// </summary>
    IReadOnlyCollection<BulletinMessageEntity> Messages { get; }

    /// <summary>
    ///     Gets whether the item is a bulletin board: its template has the script of one.
    /// </summary>
    bool IsBoard(ItemEntity item);

    /// <summary>
    ///     Opens the board on the client of the session: its expired threads go first, then the client gets the
    ///     board (0x71) and its messages as the content of a container (0x3C). False when they could not be sent.
    /// </summary>
    bool Open(ItemEntity board, GameSession session);

    /// <summary>
    ///     Gets the messages of the board: the threads from the oldest, each followed by its replies in the order
    ///     they were posted.
    /// </summary>
    IReadOnlyList<BulletinMessageEntity> GetMessages(Serial board);

    /// <summary>
    ///     Gets a message; null when there is none with that serial.
    /// </summary>
    BulletinMessageEntity? GetMessage(Serial message);

    /// <summary>
    ///     Posts on the board: a new thread, or a reply when <paramref name="replyTo" /> is a message of that board.
    ///     The subject and the lines are cleaned and cut; the poster waits between two posts unless
    ///     <paramref name="rank" /> is of the staff; a board beyond its size lets its oldest thread go.
    /// </summary>
    BulletinPostResult Post(
        ItemEntity board,
        MobileEntity poster,
        AccountType rank,
        Serial replyTo,
        string subject,
        IReadOnlyList<string> lines
    );

    /// <summary>
    ///     Posts on the board for a script, in the name given: no character is the poster, so nobody waits and only
    ///     the staff removes it. The name, the subject and the lines are cleaned and cut as a player's, and the board
    ///     keeps to its size. <see cref="BulletinPostResultType.Empty" /> without a name, a subject or a line of text.
    /// </summary>
    BulletinPostResult PostAs(
        ItemEntity board,
        string name,
        string subject,
        IReadOnlyList<string> lines,
        Serial replyTo = default
    );

    /// <summary>
    ///     Gets whether <paramref name="by" /> may remove the message: its poster, or a game master and above.
    /// </summary>
    bool CanRemove(BulletinMessageEntity message, MobileEntity by, AccountType rank);

    /// <summary>
    ///     Removes the message, and its replies when it starts a thread; the serials that went, none when there was
    ///     no such message.
    /// </summary>
    IReadOnlyList<Serial> Remove(Serial message);

    /// <summary>
    ///     Removes the threads of the board whose last reply is older than the days of the settings; the serials
    ///     that went.
    /// </summary>
    IReadOnlyList<Serial> Expire(Serial board);

    /// <summary>
    ///     Expires the threads of every board, and removes the messages of the boards that no longer exist.
    /// </summary>
    void Sweep();
}
