using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Disguises mobiles for a while: see <see cref="IDisguiseService" />. What a disguise replaced is kept in props of
///     the mobile, and a disguised mobile that was saved is given its own looks back when it comes back.
/// </summary>
public sealed class DisguiseService : IDisguiseService
{
    /// <summary>
    ///     Prop of a disguised mobile: when the disguise ends, in seconds of the Unix epoch.
    /// </summary>
    public const string UntilProp = "disguise.until";

    /// <summary>
    ///     Prop of a disguised mobile: the body it had, when the disguise changed it.
    /// </summary>
    public const string BodyProp = "disguise.body";

    /// <summary>
    ///     Prop of a disguised mobile: the skin hue it had, when the disguise changed it.
    /// </summary>
    public const string HueProp = "disguise.hue";

    /// <summary>
    ///     Prop of a disguised mobile: the name it had, when the disguise changed it.
    /// </summary>
    public const string NameProp = "disguise.name";

    private const string TimerName = "disguise";

    private readonly IMobileStateService _state;
    private readonly ITimerService _timers;
    private readonly TimeProvider _time;
    private readonly IMountService? _mounts;
    private readonly Dictionary<Serial, string> _running = [];

    public DisguiseService(IMobileStateService state, ITimerService timers, TimeProvider time, IMountService? mounts = null)
    {
        _state = state;
        _timers = timers;
        _time = time;
        _mounts = mounts;
    }

    public bool Disguise(MobileEntity mobile, DisguiseLooks looks, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        ArgumentNullException.ThrowIfNull(looks);

        if (duration <= TimeSpan.Zero ||
            mobile.IsDead ||
            IsDisguised(mobile) ||
            (looks.Body is null && looks.Hue is null && looks.Name is null))
        {
            return false;
        }

        var name = mobile.Name ?? string.Empty;
        var body = mobile.Body;
        var hue = mobile.SkinHue.Value;

        // The name may be refused (too long, blank): nothing is changed then.
        if (looks.Name is not null && !_state.SetName(mobile, looks.Name))
        {
            return false;
        }

        mobile.SetProp(UntilProp, (_time.GetUtcNow() + duration).ToUnixTimeSeconds());

        if (looks.Name is not null)
        {
            mobile.SetProp(NameProp, name);
        }

        if (looks.Body is { } newBody)
        {
            mobile.SetProp(BodyProp, (long)body);

            // A rider does not keep its horse in a body that is not a man's.
            if (!CorpseProps.IsHumanBody(newBody))
            {
                _mounts?.Dismount(mobile);
            }
        }

        if (looks.Hue is not null)
        {
            mobile.SetProp(HueProp, (long)hue);
        }

        _state.SetLooks(mobile, looks.Body, looks.Hue);
        Start(mobile, duration);

        return true;
    }

    public bool IsDisguised(MobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        return mobile.TryGetProp<long>(UntilProp, out _);
    }

    public bool End(MobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (!IsDisguised(mobile))
        {
            return false;
        }

        Stop(mobile.Id);

        if (mobile.TryGetProp<string>(NameProp, out var name))
        {
            _state.SetName(mobile, name);
        }

        int? body = mobile.TryGetProp<long>(BodyProp, out var savedBody) ? (int)savedBody : null;
        int? hue = mobile.TryGetProp<long>(HueProp, out var savedHue) ? (int)savedHue : null;

        if (body is not null || hue is not null)
        {
            _state.SetLooks(mobile, body, hue);
        }

        mobile.RemoveProp(UntilProp);
        mobile.RemoveProp(NameProp);
        mobile.RemoveProp(BodyProp);
        mobile.RemoveProp(HueProp);

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
            End(mobile);

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
                End(mobile);
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
