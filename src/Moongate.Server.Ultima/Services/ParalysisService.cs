using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Paralyzes mobiles by freezing them: see <see cref="IParalysisService" />. The end time is the prop
///     <see cref="UntilProp" />, in seconds of the Unix epoch.
/// </summary>
public sealed class ParalysisService : IParalysisService
{
    /// <summary>
    ///     Prop of a paralyzed mobile: when the paralysis ends, in seconds of the Unix epoch.
    /// </summary>
    public const string UntilProp = "paralysis.until";

    private const string TimerName = "paralysis";

    private readonly IMobileStateService _state;
    private readonly ITimerService _timers;
    private readonly TimeProvider _time;
    private readonly Dictionary<Serial, string> _running = [];

    public ParalysisService(IMobileStateService state, ITimerService timers, TimeProvider time)
    {
        _state = state;
        _timers = timers;
        _time = time;
    }

    public bool Paralyze(MobileEntity mobile, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (duration <= TimeSpan.Zero || mobile.Frozen || mobile.IsDead)
        {
            return false;
        }

        mobile.SetProp(UntilProp, (_time.GetUtcNow() + duration).ToUnixTimeSeconds());
        _state.SetFrozen(mobile, true);
        Start(mobile, duration);

        return true;
    }

    public bool IsParalyzed(MobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        return mobile.Frozen && mobile.TryGetProp<long>(UntilProp, out _);
    }

    public bool Release(MobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (!mobile.TryGetProp<long>(UntilProp, out _))
        {
            return false;
        }

        Stop(mobile.Id);
        mobile.RemoveProp(UntilProp);
        _state.SetFrozen(mobile, false);

        return true;
    }

    public void Resume(MobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (!mobile.TryGetProp<long>(UntilProp, out var until) || _running.ContainsKey(mobile.Id))
        {
            return;
        }

        var left = TimeSpan.FromSeconds(until - _time.GetUtcNow().ToUnixTimeSeconds());

        if (left <= TimeSpan.Zero)
        {
            Release(mobile);

            return;
        }

        Start(mobile, left);
    }

    private void Start(MobileEntity mobile, TimeSpan duration)
    {
        Stop(mobile.Id);
        _running[mobile.Id] = _timers.RegisterTimer(
            TimerName,
            duration,
            () =>
            {
                _running.Remove(mobile.Id);
                Release(mobile);
            }
        );
    }

    private void Stop(Serial mobile)
    {
        if (_running.Remove(mobile, out var timer))
        {
            _timers.UnregisterTimer(timer);
        }
    }
}
