using Moongate.Core.Interfaces;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.World;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;

namespace Moongate.Server.Services.World;

/// <summary>
/// Periodically realigns what each client believes with what is true.
/// <para>
/// The reactive path is what makes the world appear and disappear; this is the net under it. Any
/// mutation route that forgets to update visibility — a teleport, a map change, a future hiding
/// system — leaves a <em>ghost</em>: something the client still draws that is not there. Without a
/// sweep those survive until the player relogs, which is the classic bug of this system in every
/// emulator that only reacts.
/// </para>
/// <para>
/// It is not a second algorithm. It is the same <see cref="IVisibilityService.Refresh" />, which on
/// a correct session finds nothing and sends nothing — so the net costs a walk over the sessions and
/// no packets.
/// </para>
/// </summary>
public sealed class VisibilityReconciler : ISquidStdService
{
    private const string TimerName = "visibility-reconcile";

    /// <summary>
    /// Deliberately a constant rather than config: this is a safety net, and a knob would invite
    /// tuning it to paper over a missed trigger instead of fixing the trigger.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger = Log.ForContext<VisibilityReconciler>();
    private readonly ISessionManager _sessions;
    private readonly IVisibilityService _visibility;
    private readonly IGameLoopContext _loop;

    public VisibilityReconciler(ISessionManager sessions, IVisibilityService visibility, IGameLoopContext loop)
    {
        _sessions = sessions;
        _visibility = visibility;
        _loop = loop;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _loop.ScheduleRepeating(TimerName, Interval, Tick);

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>Public so a test can drive one beat without waiting on the timer.</summary>
    public void Tick()
    {
        var corrected = 0;

        foreach (var session in _sessions.All)
        {
            if (session.Character is null)
            {
                continue;
            }

            var delta = _visibility.Refresh(session);

            if (!delta.IsEmpty)
            {
                corrected++;
            }
        }

        // A quiet sweep is the expected case, so anything it had to fix is worth knowing about: it
        // means a reactive path missed something.
        if (corrected > 0)
        {
            _logger.Debug("Visibility reconciliation corrected {Count} session(s)", corrected);
        }
    }
}
