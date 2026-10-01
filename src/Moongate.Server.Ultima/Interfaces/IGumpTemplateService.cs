using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Types.Gumps;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Opens the gumps of <c>templates/gumps</c> from C#: filled from arguments, with the answer and the <c>on_click</c>
///     name of the button pressed handed back.
/// </summary>
public interface IGumpTemplateService
{
    /// <summary>
    ///     Gets whether <c>templates/gumps</c> has gump <paramref name="id" />.
    /// </summary>
    bool Exists(string id);

    /// <summary>
    ///     Opens gump <paramref name="id" /> on the player, its <c>${name}</c> filled from <paramref name="args" />. Call it
    ///     on the game loop.
    /// </summary>
    /// <returns>
    ///     False when there is no such gump or the session has closed.
    /// </returns>
    bool Open(
        GameSession session,
        string id,
        IReadOnlyDictionary<string, string> args,
        Action<GameSession, GumpTemplateAnswer> onAnswer,
        Action<GameSession, GumpCloseReasonType>? onClosed = null
    );

    /// <summary>
    ///     Opens gump <paramref name="id" /> and completes with the <c>on_click</c> name of the button the player presses;
    ///     null when the player closes it, presses an <c>id</c> button, the server closes it, there is no such gump, or
    ///     the session has closed.
    ///     Call it off the game loop.
    /// </summary>
    Task<string?> AskAsync(
        GameSession session,
        string id,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default
    );
}
