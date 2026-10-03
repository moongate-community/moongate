using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Prompts;

/// <summary>
///     Answers every <see cref="Begin" /> at once with <see cref="Answer" />, null for a cancel, and counts the cancels.
/// </summary>
public sealed class StubPromptService : IPromptService
{
    public string? Answer { get; set; }

    public int Cancels { get; private set; }

    public void Begin(GameSession session, Action<GameSession, string?> callback)
    {
        callback(session, Answer);
    }

    public void Cancel(GameSession session)
    {
        Cancels++;
    }

    public bool TryComplete(GameSession session, int promptId, string? text)
    {
        return false;
    }

    public void OnSessionClosed(GameSession session)
    {
    }
}
