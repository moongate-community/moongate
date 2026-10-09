using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Help;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Help;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The queue of the requests for a game master: it keeps them, tells the staff online when one arrives and gives the
///     players the answers, at once or at their next login.
/// </summary>
public interface IHelpPageService : IMoongateStartupService, IPersistenceDeletionSource
{
    /// <summary>
    ///     Gets every request in memory, closed ones included, for the world save.
    /// </summary>
    IReadOnlyCollection<HelpPageEntity> Pages { get; }

    /// <summary>
    ///     Gets how many requests are open or taken.
    /// </summary>
    int WaitingCount { get; }

    /// <summary>
    ///     Makes a request in the name of a player and tells the staff online. Runs on the game loop.
    /// </summary>
    /// <param name="player">
    ///     The character who asks.
    /// </param>
    /// <param name="kind">
    ///     What it is about.
    /// </param>
    /// <param name="text">
    ///     The line the player typed; cut at 128 characters.
    /// </param>
    HelpPageCreateResult Create(MobileEntity player, HelpPageKindType kind, string text);

    /// <summary>
    ///     Says whether a player may ask now, without asking: Ok, AlreadyOpen or Wait.
    /// </summary>
    /// <param name="player">
    ///     The serial of the character.
    /// </param>
    HelpPageCreateResult CanCreate(Serial player);

    /// <summary>
    ///     Gets the open and taken requests, the oldest first.
    /// </summary>
    IReadOnlyList<HelpPageEntity> Active();

    /// <summary>
    ///     Gets a request by its number, closed or not; null when there is none.
    /// </summary>
    /// <param name="id">
    ///     The number of the request.
    /// </param>
    HelpPageEntity? Get(Serial id);

    /// <summary>
    ///     Marks a request as taken by a game master; false when it is unknown or closed.
    /// </summary>
    /// <param name="id">
    ///     The number of the request.
    /// </param>
    /// <param name="staffName">
    ///     The name of the game master.
    /// </param>
    bool Take(Serial id, string staffName);

    /// <summary>
    ///     Answers a request and closes it; the player is told at once or at its next login. False when the request is
    ///     unknown or closed, or the answer is empty.
    /// </summary>
    /// <param name="id">
    ///     The number of the request.
    /// </param>
    /// <param name="staffName">
    ///     The name of the game master.
    /// </param>
    /// <param name="text">
    ///     The answer; cut at 128 characters.
    /// </param>
    bool Answer(Serial id, string staffName, string text);

    /// <summary>
    ///     Closes a request with no answer; false when it is unknown or already closed.
    /// </summary>
    /// <param name="id">
    ///     The number of the request.
    /// </param>
    /// <param name="staffName">
    ///     The name of the game master.
    /// </param>
    bool Close(Serial id, string staffName);
}
