using Moongate.Core.Interfaces;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Abstractions.Interfaces.Services;

namespace Moongate.Server.Services.World;

/// <summary>
/// Empties the per-session movement queues on the game loop. Steps that arrived too early to run are
/// held rather than refused, and something has to hand them back once they come due — otherwise the
/// last one sits there until the player moves again, which reads as the character stopping a step
/// short of where they walked.
/// </summary>
public sealed class MovementQueueService : ISquidStdService
{
    private const string TimerName = "movement_queue_drain";

    /// <summary>
    /// How often queues are checked. Well under the 200ms a running step costs, so a held step is
    /// handed back within a frame or two of coming due rather than visibly late.
    /// </summary>
    private static readonly TimeSpan DrainInterval = TimeSpan.FromMilliseconds(50);

    private readonly IGameLoopContext _loop;
    private readonly ISessionManager _sessions;
    private readonly IMovementService _movement;

    private string? _timerId;

    public MovementQueueService(IGameLoopContext loop, ISessionManager sessions, IMovementService movement)
    {
        _loop = loop;
        _sessions = sessions;
        _movement = movement;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _timerId ??= _loop.ScheduleRepeating(TimerName, DrainInterval, Drain);

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_timerId is not null)
        {
            _loop.Cancel(_timerId);
            _timerId = null;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A session with nothing waiting is the normal case — a client walking at a sane pace never
    /// queues — so the empty check comes before anything else.
    /// </summary>
    private void Drain()
    {
        foreach (var session in _sessions.All)
        {
            if (session.MovementQueue.Count > 0)
            {
                _movement.DrainQueue(session);
            }
        }
    }
}
