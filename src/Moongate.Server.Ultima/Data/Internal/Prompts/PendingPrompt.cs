using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Ultima.Data.Internal.Prompts;

/// <summary>
///     The line of text a player was asked for: the id its answer must carry and what to call with the text, null when
///     the player gave none.
/// </summary>
public sealed record PendingPrompt(int Id, Action<GameSession, string?> Callback);
