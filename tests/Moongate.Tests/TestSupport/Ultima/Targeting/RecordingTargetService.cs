using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Tests.TestSupport.Ultima.Targeting;

/// <summary>
///     Keeps the cursor a player was given and its callback, until a test answers it with <see cref="Answer" />.
/// </summary>
public sealed class RecordingTargetService : ITargetService
{
    private Action<GameSession, TargetResult>? _callback;
    private GameSession? _session;

    public List<(TargetCursorType Cursor, TargetFlagsType Flags)> Begun { get; } = [];

    public int Cancels { get; private set; }

    public bool Waiting => _callback is not null;

    public void Begin(
        GameSession session,
        TargetCursorType cursor,
        TargetFlagsType flags,
        Action<GameSession, TargetResult> callback
    )
    {
        Begun.Add((cursor, flags));
        _session = session;
        _callback = callback;
    }

    /// <summary>
    ///     Answers the cursor that waits with the result, as the player's click does.
    /// </summary>
    public void Answer(TargetResult result)
    {
        var callback = _callback ?? throw new InvalidOperationException("No cursor is waiting.");
        _callback = null;
        callback(_session!, result);
    }

    public Task<TargetResult> RequestAsync(
        GameSession session,
        TargetCursorType cursor,
        TargetFlagsType flags,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public void Cancel(GameSession session)
    {
        Cancels++;
        _callback = null;
    }

    public bool TryComplete(GameSession session, int cursorId, TargetResult result)
    {
        return false;
    }

    public void OnSessionClosed(GameSession session)
    {
    }
}
