using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Combat;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the poisons of mobiles with the classic numbers: a tick takes one and a share of the current hits, kept
///     within the level's bounds, and half the time the last tick's damage again. The level is the saved prop
///     <see cref="LevelProp" />; the ticks are a timer, stopped when the player leaves and started again when it comes back.
/// </summary>
public sealed class PoisonService : IPoisonService, ISessionClosedListener
{
    /// <summary>
    ///     The prop that keeps a mobile's poison level, saved with it.
    /// </summary>
    public const string LevelProp = "poison.level";

    /// <summary>
    ///     The prop that keeps how many ticks a poison has done, so that coming back does not start its count again.
    /// </summary>
    public const string TicksProp = "poison.ticks";

    private const string TimerName = "poison";
    private const int MessageEvery = 2;
    private const int SeeRange = 18;
    private const int WornOff = 502136;
    private const int FeelsIll = 1042857;
    private const int LooksIll = 1042858;

    private static readonly TimeSpan FirstTick = TimeSpan.FromSeconds(3.5);

    // Lesser, regular, greater, deadly, lethal: the least and most damage, the share of the hits, the seconds between
    // ticks and how many.
    private static readonly (int Min, int Max, double Share, double Interval, int Ticks)[] Levels =
    [
        (4, 26, 0.025, 3.0, 10), (5, 26, 0.03125, 3.0, 10), (6, 26, 0.0625, 3.0, 10), (7, 26, 0.125, 4.0, 10),
        (9, 26, 0.25, 5.0, 10)
    ];

    private readonly IMobileStateService _state;
    private readonly IDeathService _death;
    private readonly ITimerService _timers;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly ISectorService _sectors;
    private readonly ISpeechService _speech;
    private readonly CombatConfig _combat;
    private readonly Random _random;
    private readonly IMobileService? _mobiles;
    private readonly ISpellCastService? _casts;
    private readonly IParalysisService? _paralysis;
    private readonly Dictionary<Serial, Ticking> _ticking = [];

    public PoisonService(
        IMobileStateService state,
        IDeathService death,
        ITimerService timers,
        ISessionService sessions,
        IPacketSendService sender,
        ISectorService sectors,
        ISpeechService speech,
        CombatConfig combat,
        Random? random = null,
        IMobileService? mobiles = null,
        ISpellCastService? casts = null,
        IParalysisService? paralysis = null
    )
    {
        _paralysis = paralysis;
        _casts = casts;
        _state = state;
        _death = death;
        _timers = timers;
        _sessions = sessions;
        _sender = sender;
        _sectors = sectors;
        _speech = speech;
        _combat = combat;
        _random = random ?? Random.Shared;
        _mobiles = mobiles;
    }

    public PoisonResultType Apply(MobileEntity mobile, int level, MobileEntity? source = null)
    {
        if (level < 0 || level >= Levels.Length || mobile.IsDead)
        {
            return PoisonResultType.Refused;
        }

        if (LevelOf(mobile) >= level)
        {
            return PoisonResultType.HigherActive;
        }

        Stop(mobile.Id);
        mobile.SetProp(LevelProp, (long)level);
        mobile.RemoveProp(TicksProp);
        Start(mobile, level, 0, source);
        TellIll(mobile, level);

        return PoisonResultType.Poisoned;
    }

    public bool Cure(MobileEntity mobile)
    {
        if (LevelOf(mobile) is null)
        {
            return false;
        }

        End(mobile);

        return true;
    }

    public int? LevelOf(MobileEntity mobile)
    {
        return ReadLevel(mobile);
    }

    /// <summary>
    ///     Gets whether the mobile's saved poison level reads as a poison; a corrupt one does not.
    /// </summary>
    public static bool IsPoisoned(MobileEntity mobile)
    {
        return ReadLevel(mobile) is not null;
    }

    /// <summary>
    ///     Gets the level of the mobile's saved poison; null when it has none or one that does not read.
    /// </summary>
    public static int? ReadLevel(MobileEntity mobile)
    {
        return ReadNumber(mobile, LevelProp) is { } level && level >= 0 && level < Levels.Length ? level : null;
    }

    public void Resume(MobileEntity mobile)
    {
        if (_ticking.ContainsKey(mobile.Id) || LevelOf(mobile) is not { } level)
        {
            return;
        }

        Start(mobile, level, ReadNumber(mobile, TicksProp) ?? 0, null);
    }

    // The ticks stop with the session; the level stays with the character for its next login.
    public void OnSessionClosed(GameSession session)
    {
        if (session.CharacterId.IsValid)
        {
            Stop(session.CharacterId);
        }
    }

    private void Start(MobileEntity mobile, int level, int count, MobileEntity? source)
    {
        var ticking = new Ticking(level) { Count = count, Source = source };
        _ticking[mobile.Id] = ticking;
        ticking.Timer = _timers.RegisterTimer(
            TimerName,
            TimeSpan.FromSeconds(Levels[level].Interval),
            () => Tick(mobile, ticking),
            FirstTick,
            true
        );
        ShowBar(mobile, level + 1);
    }

    private void Tick(MobileEntity mobile, Ticking ticking)
    {
        if (!_ticking.TryGetValue(mobile.Id, out var current) || current != ticking)
        {
            return;
        }

        // Gone from the world, as a removed NPC: nothing more is shown of it.
        if (_mobiles?.IsInWorld(mobile.Id) == false)
        {
            Stop(mobile.Id);

            return;
        }

        // Ended elsewhere, as by a death, or dead before this tick.
        if (LevelOf(mobile) != ticking.Level || mobile.IsDead)
        {
            End(mobile);

            return;
        }

        var level = Levels[ticking.Level];
        mobile.SetProp(TicksProp, (long)(ticking.Count + 1));

        if (ticking.Count++ == level.Ticks)
        {
            _speech.TellCliloc(mobile, WornOff);
            End(mobile);

            return;
        }

        var damage = ticking.LastDamage != 0 && _random.Next(2) == 0
            ? ticking.LastDamage
            : Math.Clamp(1 + (int)(mobile.Hits * level.Share), level.Min, level.Max);
        ticking.LastDamage = damage;
        ShowDamage(mobile, damage);

        // Every damage breaks a paralysis, a poison's too.
        _paralysis?.Release(mobile);

        if (mobile.Hits - damage > 0)
        {
            _state.SetStats(mobile, new MobileStatsChange { Hits = mobile.Hits - damage });

            // Every damage ruins a cast in its delay, a poison's too.
            _casts?.Hurt(mobile);

            if (ticking.Count % MessageEvery == 0)
            {
                TellIll(mobile, ticking.Level);
            }

            ShowBar(mobile, ticking.Level + 1);

            return;
        }

        _state.SetStats(mobile, new MobileStatsChange { Hits = 0 });
        End(mobile);

        // A player that cannot die, such as one with a body that has no ghost, is left with one hit point.
        if (!_death.Kill(mobile, ticking.Source) && !mobile.IsNpc)
        {
            _state.SetStats(mobile, new MobileStatsChange { Hits = 1 });
        }
    }

    private void End(MobileEntity mobile)
    {
        Stop(mobile.Id);
        mobile.RemoveProp(LevelProp);
        mobile.RemoveProp(TicksProp);
        ShowBar(mobile, 0);
    }

    private void Stop(Serial mobile)
    {
        if (_ticking.Remove(mobile, out var ticking) && ticking.Timer is { } timer)
        {
            _timers.UnregisterTimer(timer);
        }
    }

    // Its own player feels ill, those around see it look ill: the stronger the poison, the stronger the words.
    private void TellIll(MobileEntity mobile, int level)
    {
        foreach (var other in Around(mobile))
        {
            if (other.Id == mobile.Id)
            {
                _speech.SayClilocTo(mobile, mobile, FeelsIll + level * 2);
            }
            else
            {
                _speech.SayClilocTo(mobile, other, LooksIll + level * 2, mobile.Name);
            }
        }
    }

    private void ShowBar(MobileEntity mobile, int level)
    {
        var bar = new HealthBarStatusPacket(mobile.Id.Value, HealthBarType.Poison, level);

        foreach (var other in Around(mobile))
        {
            if (_sessions.TryGetByCharacterId(other.Id, out var session))
            {
                _sender.TrySend(session.SessionId, bar);
            }
        }
    }

    private void ShowDamage(MobileEntity mobile, int damage)
    {
        if (_combat.DisplayDamageNumbers && !mobile.IsNpc && _sessions.TryGetByCharacterId(mobile.Id, out var session))
        {
            _sender.TrySend(session.SessionId, new DamagePacket(mobile.Id, damage));
        }
    }

    // The players who see it: a hidden mobile is seen by its own player and the staff only.
    private IEnumerable<MobileEntity> Around(MobileEntity mobile)
    {
        return _sectors.GetMobilesInRange(mobile.Map, mobile.Location, SeeRange)
            .Where(other => !other.IsNpc && (!mobile.Hidden || other.Id == mobile.Id || IsStaff(other)));
    }

    private bool IsStaff(MobileEntity player)
    {
        return _sessions.TryGetByCharacterId(player.Id, out var session) && session.AccountType >= AccountType.GameMaster;
    }

    private static int? ReadNumber(MobileEntity mobile, string key)
    {
        try
        {
            return mobile.TryGetProp<long>(key, out var value) && value is >= 0 and <= int.MaxValue ? (int)value : null;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return null;
        }
    }

    private sealed class Ticking
    {
        public Ticking(int level)
        {
            Level = level;
        }

        public int Level { get; }

        public int Count { get; set; }

        public int LastDamage { get; set; }

        public string? Timer { get; set; }

        public MobileEntity? Source { get; init; }
    }
}
