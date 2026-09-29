using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Tests.TestSupport.Ultima.Targeting;

/// <summary>
///     Answers every <see cref="RequestAsync" /> with <see cref="Result" /> and counts the requests.
/// </summary>
public sealed class StubTargetService : ITargetService
{
    public TargetResult Result { get; set; } = TargetResult.Canceled(TargetCancelType.Canceled);

    public int Requests { get; private set; }

    public void Begin(
        GameSession session,
        TargetCursorType cursor,
        TargetFlagsType flags,
        Action<GameSession, TargetResult> callback
    )
    {
        callback(session, Result);
    }

    public Task<TargetResult> RequestAsync(
        GameSession session,
        TargetCursorType cursor,
        TargetFlagsType flags,
        CancellationToken cancellationToken = default
    )
    {
        Requests++;

        return Task.FromResult(Result);
    }

    public void Cancel(GameSession session)
    {
    }

    public bool TryComplete(GameSession session, int cursorId, TargetResult result)
    {
        return false;
    }

    public void OnSessionClosed(GameSession session)
    {
    }
}
