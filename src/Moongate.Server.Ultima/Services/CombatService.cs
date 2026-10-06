using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Combat;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Packets.Combat;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Melee combat, as ModernUO's classic one. Each fighter has a target and a timer: every tenth of a second the
///     fighters whose delay is over, and whose target is within reach, swing. A swing plays its animation and sound,
///     rolls the hit with the Wrestling of both, does the damage and, when the target is an NPC left with no hit points,
///     kills it. The victim NPC fights back.
/// </summary>
public sealed class CombatService : ICombatService
{
    public const string TimerName = "combat";
    public const int TickMilliseconds = 100;

    /// <summary>
    ///     The speed of fists, and so of every swing until the weapons of items exist.
    /// </summary>
    public const int FistsSpeed = 30;

    public const int FistsHitSound = 0x135;
    public const int MissSound = 0x239;

    // How far apart in height two fighters can be and still reach each other.
    private const int ReachInHeight = 15;
    private const int FistsMinimumDamage = 1;
    private const int FistsMaximumDamage = 8;
    private const int SwingFrames = 7;
    private const int OtherSwingFrames = 5;
    private const int HurtFrames = 5;
    private const int MonsterHurtFrames = 4;
    private const double Tenth = 10.0;
    private const double PassiveMinimum = 0;
    private const double PassiveMaximum = 100;

    private readonly ILogger _logger = Log.ForContext<CombatService>();
    private readonly Dictionary<Serial, Fighter> _fighters = [];
    private readonly Lazy<Dictionary<int, BodyType>> _bodies;
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly IMobileTemplateService _templates;
    private readonly ISkillService _skills;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly IWorldViewService _view;
    private readonly ISpeechService _speech;
    private readonly IDeathService _death;
    private readonly ICrimeService _crimes;
    private readonly ILineOfSightService _sight;
    private readonly ITimerService _timers;
    private readonly CombatConfig _config;
    private readonly WorldConfig _world;
    private readonly TimeProvider _time;
    private readonly Random _random;
    private string? _timerId;

    public CombatService(
        IMobileService mobiles,
        IMobileStateService state,
        IMobileTemplateService templates,
        ISkillService skills,
        ISessionService sessions,
        IPacketSendService sender,
        IWorldViewService view,
        ISpeechService speech,
        IDeathService death,
        ICrimeService crimes,
        ILineOfSightService sight,
        IDataLoaderService data,
        ITimerService timers,
        CombatConfig config,
        WorldConfig world,
        TimeProvider time,
        Random? random = null
    )
    {
        _mobiles = mobiles;
        _state = state;
        _templates = templates;
        _skills = skills;
        _sessions = sessions;
        _sender = sender;
        _view = view;
        _speech = speech;
        _death = death;
        _crimes = crimes;
        _sight = sight;
        _timers = timers;
        _config = config;
        _world = world;
        _time = time;
        _random = random ?? Random.Shared;
        _bodies = new(() => data.GetEntities<BodyContent>().ToDictionary(body => (int)body.Body.Value, body => body.Type));
    }

    public Task StartAsync()
    {
        _timerId = _timers.RegisterTimer(
            TimerName,
            TimeSpan.FromMilliseconds(TickMilliseconds),
            Tick,
            TimeSpan.FromMilliseconds(TickMilliseconds),
            true
        );

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_timerId is { } id)
        {
            _timers.UnregisterTimer(id);
            _timerId = null;
        }

        _fighters.Clear();

        return Task.CompletedTask;
    }

    public bool Attack(MobileEntity attacker, MobileEntity target)
    {
        if (!CanAttack(attacker, target))
        {
            return false;
        }

        if (!attacker.IsNpc)
        {
            _state.SetWarMode(attacker, true);
        }

        // A player who goes for an innocent that is not fighting it is a criminal, and the guards come.
        if (!attacker.IsNpc &&
            target.ShownNotoriety == NotorietyType.Innocent &&
            TargetOf(target)?.Id != attacker.Id &&
            TargetOf(attacker)?.Id != target.Id)
        {
            _crimes.MakeCriminal(attacker);
        }

        var now = _time.GetUtcNow();
        Fight(attacker, target, now, true);

        return true;
    }

    public void Stop(MobileEntity mobile)
    {
        if (_fighters.Remove(mobile.Id))
        {
            Tell(mobile, Serial.Zero);
        }
    }

    public MobileEntity? TargetOf(MobileEntity mobile)
    {
        return _fighters.TryGetValue(mobile.Id, out var fighter) ? fighter.Target : null;
    }

    // The fight starts or changes: the first swing is at once, as ModernUO's fighter with no weapon delay.
    private void Fight(MobileEntity attacker, MobileEntity target, DateTimeOffset now, bool tell)
    {
        var expires = now.AddSeconds(_config.CombatantSeconds);

        if (_fighters.TryGetValue(attacker.Id, out var known))
        {
            if (known.Target.Id == target.Id)
            {
                known.ExpiresAt = expires;

                return;
            }

            known.Target = target;
            known.NextSwingAt = now;
            known.ExpiresAt = expires;
        }
        else
        {
            _fighters[attacker.Id] = new() { Attacker = attacker, Target = target, NextSwingAt = now, ExpiresAt = expires };
        }

        if (tell)
        {
            Tell(attacker, target.Id);
        }
    }

    private bool CanAttack(MobileEntity attacker, MobileEntity target)
    {
        if (attacker.Id == target.Id ||
            target.Notoriety == NotorietyType.Invulnerable ||
            target.Hits <= 0 ||
            !_mobiles.IsInWorld(attacker.Id) ||
            !_mobiles.IsInWorld(target.Id) ||
            attacker.Map != target.Map)
        {
            return false;
        }

        if (target.IsHiddenFrom(attacker.Id, AccountOf(attacker)))
        {
            return false;
        }

        if (!attacker.Location.InRange(target.Location, _world.ViewRange))
        {
            return false;
        }

        try
        {
            return _sight.HasLineOfSight(attacker.Map, attacker.Location, target.Location);
        }
        catch (KeyNotFoundException)
        {
            // A map that is not loaded cannot be fought on.
            return false;
        }
    }

    private AccountType AccountOf(MobileEntity mobile)
    {
        return !mobile.IsNpc && _sessions.TryGetByCharacterId(mobile.Id, out var session)
                   ? session.AccountType
                   : AccountType.Regular;
    }

    // A timer callback that throws closes the timer wheel.
    private void Tick()
    {
        try
        {
            var now = _time.GetUtcNow();

            foreach (var fighter in _fighters.Values.ToArray())
            {
                // One that fails is stopped, so the others still swing and it does not fail at every tick.
                try
                {
                    Step(fighter, now);
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "The fight of {Attacker} against {Target} failed", fighter.Attacker, fighter.Target);
                    Stop(fighter.Attacker);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "The combat tick failed");
        }
    }

    private void Step(Fighter fighter, DateTimeOffset now)
    {
        var (attacker, target) = (fighter.Attacker, fighter.Target);

        if (!_mobiles.IsInWorld(attacker.Id) ||
            !_mobiles.IsInWorld(target.Id) ||
            attacker.Map != target.Map ||
            now >= fighter.ExpiresAt ||
            // An NPC with no hit points is dying: the dead do not fight, nor are they fought.
            attacker.IsNpc && attacker.Hits <= 0 ||
            target.IsNpc && target.Hits <= 0 ||
            !attacker.IsNpc && !attacker.WarMode)
        {
            Stop(attacker);

            return;
        }

        if (!InReach(attacker, target) || now < fighter.NextSwingAt)
        {
            return;
        }

        Swing(fighter, now);
    }

    private bool InReach(MobileEntity attacker, MobileEntity target)
    {
        return attacker.Location.InRange(target.Location, _config.MaxRange) &&
               Math.Abs(attacker.Location.Z - target.Location.Z) <= ReachInHeight;
    }

    private void Swing(Fighter fighter, DateTimeOffset now)
    {
        var (attacker, target) = (fighter.Attacker, fighter.Target);

        // Swinging shows who hid.
        if (attacker.Hidden)
        {
            _state.SetHidden(attacker, false);
        }

        fighter.NextSwingAt = now.AddSeconds(CombatFormulas.SwingDelaySeconds(attacker.Stamina, FistsSpeed, _config.GlobalAttackSpeed));
        fighter.ExpiresAt = now.AddSeconds(_config.CombatantSeconds);
        PayStamina(attacker);

        if (_sessions.TryGetByCharacterId(attacker.Id, out var own))
        {
            _sender.TrySend(own.SessionId, new SwingPacket(attacker.Id, target.Id));
        }

        var (action, frames) = SwingAnimation(attacker);
        _view.MobileAnimated(attacker, action, frames, 1);

        var chance = CombatFormulas.HitChance(Wrestling(attacker), Wrestling(target));

        if (!_skills.CheckChance(attacker, SkillType.Wrestling, chance))
        {
            _speech.PlaySound(attacker, MissSound);
            FightBack(target, attacker, now);

            return;
        }

        _speech.PlaySound(attacker, SoundsOf(attacker)?.Attack is { } sound and > 0 ? sound : FistsHitSound);
        Hit(attacker, target, now);
    }

    private void PayStamina(MobileEntity attacker)
    {
        if (attacker.IsNpc || _config.AttackStamina == 0)
        {
            return;
        }

        _state.SetStats(attacker, new MobileStatsChange { Stamina = Math.Max(attacker.Stamina - _config.AttackStamina, 0) });
    }

    private void Hit(MobileEntity attacker, MobileEntity target, DateTimeOffset now)
    {
        var damage = DamageOf(attacker, target);

        if (SoundsOf(target)?.Hurt is { } hurt and > 0)
        {
            _speech.PlaySound(target, hurt);
        }

        var (action, frames) = HurtAnimation(target);
        _view.MobileAnimated(target, action, frames, 1);
        ShowDamage(attacker, target, damage);

        // A player cannot die yet: it is left with one hit point.
        var hits = target.IsNpc ? target.Hits - damage : Math.Max(target.Hits - damage, 1);

        if (hits > 0)
        {
            _state.SetStats(target, new MobileStatsChange { Hits = hits });
            FightBack(target, attacker, now);

            return;
        }

        _state.SetStats(target, new MobileStatsChange { Hits = 0 });
        _death.Kill(target, attacker);
        Stop(attacker);
        Stop(target);
    }

    // The NPC that is hit, or missed, fights the one who swings, if it fights no one; whoever hit it keeps it at it.
    private void FightBack(MobileEntity victim, MobileEntity attacker, DateTimeOffset now)
    {
        if (!victim.IsNpc || !_mobiles.IsInWorld(victim.Id))
        {
            return;
        }

        if (!_fighters.ContainsKey(victim.Id))
        {
            Fight(victim, attacker, now, false);
        }
    }

    private int DamageOf(MobileEntity attacker, MobileEntity target)
    {
        var damage = Math.Max(BaseDamage(attacker), FistsMinimumDamage);
        // Tactics and anatomy are tried at every hit: they teach a player as they are used.
        _skills.Check(attacker, SkillType.Tactics, PassiveMinimum, PassiveMaximum);
        _skills.Check(attacker, SkillType.Anatomy, PassiveMinimum, PassiveMaximum);
        damage = CombatFormulas.ScaleDamage(
            damage,
            Points(attacker, SkillType.Tactics),
            attacker.Strength,
            Points(attacker, SkillType.Anatomy)
        );
        // As ModernUO's classic: a player hit, or a hit by an NPC, does half; a player hitting an NPC does all.
        var halved = !target.IsNpc || attacker.IsNpc;
        var rate = attacker.IsNpc && !target.IsNpc ? _config.NpcDamageRate : 1.0;

        return CombatFormulas.Final(CombatFormulas.Reduce(damage, halved, rate), target.Armor, _random);
    }

    // The dice of the template of an NPC, else the fists of ModernUO.
    private int BaseDamage(MobileEntity attacker)
    {
        if (attacker.IsNpc &&
            attacker.TemplateId is { } id &&
            _templates.TryGet(id, out var template) &&
            template.Damage is { } dice)
        {
            return dice.Roll();
        }

        return _random.Next(FistsMaximumDamage) + FistsMinimumDamage;
    }

    private void ShowDamage(MobileEntity attacker, MobileEntity target, int damage)
    {
        if (!_config.DisplayDamageNumbers)
        {
            return;
        }

        foreach (var player in new[] { attacker, target })
        {
            if (!player.IsNpc && _sessions.TryGetByCharacterId(player.Id, out var session))
            {
                _sender.TrySend(session.SessionId, new DamagePacket(target.Id, damage));
            }
        }
    }

    private void Tell(MobileEntity mobile, Serial target)
    {
        if (!mobile.IsNpc && _sessions.TryGetByCharacterId(mobile.Id, out var session))
        {
            _sender.TrySend(session.SessionId, new CombatantPacket(target));
        }
    }

    // The sounds of the template of an NPC; a player has its own, which the client plays.
    private MobileSounds? SoundsOf(MobileEntity mobile)
    {
        return mobile.IsNpc && mobile.TemplateId is { } id && _templates.TryGet(id, out var template) ? template.Sounds : null;
    }

    private BodyType BodyOf(MobileEntity mobile)
    {
        return _bodies.Value.GetValueOrDefault(mobile.Body, BodyType.Monster);
    }

    private (int Action, int Frames) SwingAnimation(MobileEntity attacker)
    {
        return BodyOf(attacker) switch
        {
            BodyType.Human  => ((int)HumanAnimationType.Punch, SwingFrames),
            BodyType.Animal => ((int)AnimalAnimationType.Attack1, OtherSwingFrames),
            _               => ((int)MonsterAnimationType.Attack1, OtherSwingFrames)
        };
    }

    private (int Action, int Frames) HurtAnimation(MobileEntity target)
    {
        return BodyOf(target) switch
        {
            BodyType.Human  => ((int)HumanAnimationType.GetHit, HurtFrames),
            BodyType.Animal => ((int)AnimalAnimationType.GetHit, HurtFrames),
            _               => ((int)MonsterAnimationType.GetHit, MonsterHurtFrames)
        };
    }

    private static double Wrestling(MobileEntity mobile)
    {
        return Points(mobile, SkillType.Wrestling);
    }

    // The points of a skill, whole and tenths, as 50.5; 0 for one the mobile does not have.
    private static double Points(MobileEntity mobile, SkillType skill)
    {
        return (mobile.Skills.FirstOrDefault(known => known.Skill == skill)?.Base ?? 0) / Tenth;
    }
}
