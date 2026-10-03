using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Sessions;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Asks players for a line of text with the text prompt (0xC2) and hands what they typed to a callback on the game
///     loop. A player has one prompt at a time.
/// </summary>
public interface IPromptService : ISessionClosedListener
{
    /// <summary>
    ///     Shows the player the prompt; <paramref name="callback" /> runs on the game loop with the text, or with null
    ///     when the player escaped, was asked something else, was canceled or left. Without a character in the world
    ///     the callback gets null at once. Call it on the game loop.
    /// </summary>
    void Begin(GameSession session, Action<GameSession, string?> callback);

    /// <summary>
    ///     Stops waiting for the player's answer and ends the prompt with null. The client has no packet that closes
    ///     its prompt, so a late answer is ignored. Call it on the game loop.
    /// </summary>
    void Cancel(GameSession session);

    /// <summary>
    ///     Ends the player's prompt with <paramref name="text" /> when its id is <paramref name="promptId" />; false,
    ///     and nothing happens, when no prompt with that id is waiting. Call it on the game loop.
    /// </summary>
    bool TryComplete(GameSession session, int promptId, string? text);
}
