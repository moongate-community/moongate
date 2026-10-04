using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the criminals: <see cref="MobileEntity.CriminalUntil" /> is the saved time, <see cref="MobileEntity.Criminal" />
///     the flag the packets read. One repeating <c>crime</c> timer, every second, clears those whose time is over and
///     gives the flag back to a player that came back into the world with time left.
/// </summary>
public sealed class CrimeService : ICrimeService, IMoongateStartupService
{
    public const string TimerName = "crime";

    /// <summary>
    ///     The client's "You've committed a criminal act!!"
    /// </summary>
    public const int CriminalMessage = 1005040;

    private const int MessageHue = 0x22;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger = Log.ForContext<CrimeService>();
    private readonly ITimerService _timers;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IWorldViewService _view;
    private readonly ISpeechService _speech;
    private readonly CrimeConfig _config;
    private readonly TimeProvider _time;

    // The criminals in the world, players and NPCs. Game loop only.
    private readonly HashSet<MobileEntity> _criminals = [];

    private string? _timerId;

    public CrimeService(
        ITimerService timers,
        ISessionService sessions,
        IMobileService mobiles,
        IWorldViewService view,
        ISpeechService speech,
        CrimeConfig config,
        TimeProvider time
    )
    {
        _timers = timers;
        _sessions = sessions;
        _mobiles = mobiles;
        _view = view;
        _speech = speech;
        _config = config;
        _time = time;
    }

    public Task StartAsync()
    {
        _timerId = _timers.RegisterTimer(TimerName, CheckInterval, Check, CheckInterval, true);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_timerId is { } id)
        {
            _timers.UnregisterTimer(id);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    public bool IsCriminal(MobileEntity mobile)
    {
        return mobile.Criminal;
    }

    public void MakeCriminal(MobileEntity mobile)
    {
        mobile.CriminalUntil = Now().AddSeconds(_config.CriminalSeconds);

        if (Flag(mobile))
        {
            _speech.TellCliloc(mobile, CriminalMessage, "", MessageHue);
        }
    }

    public void Restore(MobileEntity mobile)
    {
        if (mobile.CriminalUntil is not { } until)
        {
            return;
        }

        if (until <= Now())
        {
            mobile.CriminalUntil = null;

            return;
        }

        mobile.Criminal = true;
        _criminals.Add(mobile);
    }

    public void Pardon(MobileEntity mobile)
    {
        mobile.CriminalUntil = null;
        _criminals.Remove(mobile);

        if (mobile.Criminal)
        {
            mobile.Criminal = false;
            _view.MobileFlagsChanged(mobile);
        }
    }

    // Whether the mobile was not a criminal before.
    private bool Flag(MobileEntity mobile)
    {
        _criminals.Add(mobile);

        if (mobile.Criminal)
        {
            return false;
        }

        mobile.Criminal = true;
        _view.MobileFlagsChanged(mobile);

        return true;
    }

    // A timer callback that throws closes the timer wheel: one bad mobile must not stop the server.
    private void Check()
    {
        var now = Now();

        foreach (var criminal in _criminals.ToArray())
        {
            try
            {
                if (!_mobiles.TryGet(criminal.Id, out var live) || !ReferenceEquals(live, criminal))
                {
                    // Gone with time left, or back as another object: its row keeps the time, and the live object
                    // gets the flag back. Showing this one again would draw it where it left.
                    _criminals.Remove(criminal);
                }
                else if (criminal.CriminalUntil is not { } until || until <= now)
                {
                    Pardon(criminal);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The crime check of {Mobile} failed", criminal.Id);
            }
        }

        foreach (var session in _sessions.GetAll())
        {
            try
            {
                if (!session.CharacterId.IsValid ||
                    !_mobiles.TryGet(session.CharacterId, out var character) ||
                    character.Criminal ||
                    character.CriminalUntil is not { } until)
                {
                    continue;
                }

                if (until > now)
                {
                    Flag(character);
                }
                else
                {
                    character.CriminalUntil = null;
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The crime check of {Character} failed", session.CharacterId);
            }
        }
    }

    private DateTime Now()
    {
        return _time.GetUtcNow().UtcDateTime;
    }
}
