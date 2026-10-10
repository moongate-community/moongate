using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the timed stat bonuses and the night sight of mobiles in memory, each ended by its own timer or by the
///     player leaving: nothing of them is saved, so a restart leaves none behind.
/// </summary>
public sealed class StatBonusService : IStatBonusService, ISessionClosedListener
{
    private const string BonusTimer = "stat_bonus";
    private const string CurseTimer = "stat_curse";
    private const string NightSightTimer = "night_sight";
    private const int MaxLight = 30;

    private readonly IMobileStateService _state;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly ITimerService _timers;
    private readonly IMobileService _mobiles;
    private readonly Dictionary<(Serial Mobile, StatBonusType Stat), (string Timer, int Amount)> _bonuses = [];
    private readonly Dictionary<(Serial Mobile, StatBonusType Stat), (string Timer, int Amount)> _curses = [];
    private readonly Dictionary<Serial, (string Timer, int Level)> _nightSight = [];

    public StatBonusService(
        IMobileStateService state,
        ISessionService sessions,
        IPacketSendService sender,
        ITimerService timers,
        IMobileService mobiles
    )
    {
        _state = state;
        _sessions = sessions;
        _sender = sender;
        _timers = timers;
        _mobiles = mobiles;
    }

    public bool TryAddBonus(MobileEntity mobile, StatBonusType stat, int amount, TimeSpan duration)
    {
        if (amount <= 0 || duration <= TimeSpan.Zero || _bonuses.ContainsKey((mobile.Id, stat)))
        {
            return false;
        }

        _bonuses[(mobile.Id, stat)] = (_timers.RegisterTimer(BonusTimer, duration, () => EndBonus(mobile, stat)), amount);
        SetBonus(mobile, stat);
        ShowStatus(mobile);
        _state.SendHits(mobile);

        return true;
    }

    public bool TryAddBuff(MobileEntity mobile, StatBonusType stat, int amount, TimeSpan duration)
    {
        if (amount <= 0 || duration <= TimeSpan.Zero)
        {
            return false;
        }

        if (_bonuses.TryGetValue((mobile.Id, stat), out var current))
        {
            // The stronger buff of the stat wins, the one that is there already when it is as strong.
            if (current.Amount >= amount)
            {
                return false;
            }

            _timers.UnregisterTimer(current.Timer);
            _bonuses.Remove((mobile.Id, stat));
        }

        return TryAddBonus(mobile, stat, amount, duration);
    }

    public bool TryAddCurse(MobileEntity mobile, StatBonusType stat, int amount, TimeSpan duration)
    {
        if (amount <= 0 || duration <= TimeSpan.Zero)
        {
            return false;
        }

        if (_curses.TryGetValue((mobile.Id, stat), out var current))
        {
            // The stronger curse of the stat wins, the one that is there already when it is as strong.
            if (current.Amount >= amount)
            {
                return false;
            }

            _timers.UnregisterTimer(current.Timer);
        }

        _curses[(mobile.Id, stat)] = (_timers.RegisterTimer(CurseTimer, duration, () => EndCurse(mobile, stat)), amount);
        SetBonus(mobile, stat);
        Clamp(mobile);
        ShowStatus(mobile);
        _state.SendHits(mobile);

        return true;
    }

    public int Bonus(MobileEntity mobile, StatBonusType stat)
    {
        return stat switch
        {
            StatBonusType.Strength => mobile.StrengthBonus,
            StatBonusType.Dexterity => mobile.DexterityBonus,
            _                       => mobile.IntelligenceBonus
        };
    }

    public bool TrySetNightSight(MobileEntity mobile, int level, TimeSpan duration)
    {
        if (level is < 0 or > MaxLight || duration <= TimeSpan.Zero || _nightSight.ContainsKey(mobile.Id))
        {
            return false;
        }

        _nightSight[mobile.Id] = (_timers.RegisterTimer(NightSightTimer, duration, () => EndNightSight(mobile)), level);
        SendLight(mobile, level);

        return true;
    }

    public bool HasNightSight(MobileEntity mobile)
    {
        return _nightSight.ContainsKey(mobile.Id);
    }

    public void EndAll(MobileEntity mobile)
    {
        Forget(mobile.Id);
        mobile.StrengthBonus = 0;
        mobile.DexterityBonus = 0;
        mobile.IntelligenceBonus = 0;
        mobile.Hits = Math.Min(mobile.Hits, mobile.EffectiveHitsMax);
        mobile.Stamina = Math.Min(mobile.Stamina, mobile.EffectiveStaminaMax);
        mobile.Mana = Math.Min(mobile.Mana, mobile.EffectiveManaMax);
    }

    // The player may have left the world already: its effects are found by its serial alone.
    public void OnSessionClosed(GameSession session)
    {
        if (!session.CharacterId.IsValid)
        {
            return;
        }

        if (_mobiles.TryGet(session.CharacterId, out var mobile))
        {
            EndAll(mobile);
        }
        else
        {
            Forget(session.CharacterId);
        }
    }

    // The client may lose its personal light with the region or the map: it is sent again.
    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        if (_nightSight.TryGetValue(player.Id, out var light))
        {
            SendLight(player, light.Level);
        }
    }

    public void Left(Serial player)
    {
    }

    private void Forget(Serial mobile)
    {
        foreach (var stat in Enum.GetValues<StatBonusType>())
        {
            if (_bonuses.Remove((mobile, stat), out var bonus))
            {
                _timers.UnregisterTimer(bonus.Timer);
            }

            if (_curses.Remove((mobile, stat), out var curse))
            {
                _timers.UnregisterTimer(curse.Timer);
            }
        }

        if (_nightSight.Remove(mobile, out var light))
        {
            _timers.UnregisterTimer(light.Timer);
        }
    }

    private void EndBonus(MobileEntity mobile, StatBonusType stat)
    {
        if (!_bonuses.Remove((mobile.Id, stat)))
        {
            return;
        }

        SetBonus(mobile, stat);
        Clamp(mobile);
        ShowStatus(mobile);
        _state.SendHits(mobile);
    }

    private void EndCurse(MobileEntity mobile, StatBonusType stat)
    {
        if (!_curses.Remove((mobile.Id, stat)))
        {
            return;
        }

        SetBonus(mobile, stat);
        ShowStatus(mobile);
        _state.SendHits(mobile);
    }

    private void EndNightSight(MobileEntity mobile)
    {
        if (_nightSight.Remove(mobile.Id))
        {
            SendLight(mobile, 0);
        }
    }

    // What the stat is moved by: the bonus it has less the curse it is under.
    private void SetBonus(MobileEntity mobile, StatBonusType stat)
    {
        var amount = (_bonuses.TryGetValue((mobile.Id, stat), out var bonus) ? bonus.Amount : 0) -
                     (_curses.TryGetValue((mobile.Id, stat), out var curse) ? curse.Amount : 0);

        switch (stat)
        {
            case StatBonusType.Strength:
                mobile.StrengthBonus = amount;

                break;
            case StatBonusType.Dexterity:
                mobile.DexterityBonus = amount;

                break;
            default:
                mobile.IntelligenceBonus = amount;

                break;
        }
    }

    // Hits and stamina above the maximum the bonus held up go with it.
    private void Clamp(MobileEntity mobile)
    {
        _state.SetStats(
            mobile,
            new MobileStatsChange
            {
                Hits = Math.Min(mobile.Hits, mobile.EffectiveHitsMax),
                Stamina = Math.Min(mobile.Stamina, mobile.EffectiveStaminaMax),
                Mana = Math.Min(mobile.Mana, mobile.EffectiveManaMax)
            }
        );
    }

    private void ShowStatus(MobileEntity mobile)
    {
        if (_sessions.TryGetByCharacterId(mobile.Id, out var session))
        {
            _state.SendStatus(session, mobile);
        }
    }

    private void SendLight(MobileEntity mobile, int level)
    {
        if (_sessions.TryGetByCharacterId(mobile.Id, out var session))
        {
            _sender.TrySend(session.SessionId, new PersonalLightLevelPacket(mobile.Id, level));
        }
    }
}
