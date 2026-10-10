using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Data.Internal.Combat;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Packets.Combat;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Items;
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
    /// <summary>
    ///     The script id of the animals that run from a blow instead of fighting back.
    /// </summary>
    public const string ScaredAnimalScript = "scared_animal";

    /// <summary>
    ///     The prop a script sets to true on an NPC that must not answer a blow, as one that flees.
    /// </summary>
    public const string PassiveProp = "combat.passive";

    private const int ReachInHeight = 15;
    private const int SwingFrames = 7;
    private const byte ProjectileSpeed = 18;
    private const int EyeHeight = 14;
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
    private readonly ICombatGearService _gear;
    private readonly ITimerService _timers;
    private readonly CombatConfig _config;
    private readonly WorldConfig _world;
    private readonly TimeProvider _time;
    private readonly Random _random;
    private readonly IMurderService? _murders;
    private readonly IEffectService? _effects;
    private readonly IAmmoService? _ammo;
    private readonly IBloodService? _blood;
    private readonly IMountService? _mounts;
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
        ICombatGearService gear,
        IDataLoaderService data,
        ITimerService timers,
        CombatConfig config,
        WorldConfig world,
        TimeProvider time,
        Random? random = null,
        IMurderService? murders = null,
        IEffectService? effects = null,
        IAmmoService? ammo = null,
        IBloodService? blood = null,
        IMountService? mounts = null
    )
    {
        _mounts = mounts;
        _ammo = ammo;
        _blood = blood;
        _effects = effects;
        _murders = murders;
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
        _gear = gear;
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
        if (Refusal(attacker, target) is { } reason)
        {
            _logger.Information("{Attacker} cannot attack {Target}: {Reason}", attacker, target, reason);

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
            _murders?.Aggressed(attacker, target);
        }

        var now = _time.GetUtcNow();
        Fight(attacker, target, now, true);

        return true;
    }

    public bool Harm(MobileEntity? attacker, MobileEntity target, int damage)
    {
        if (damage < 0 ||
            target.Notoriety == NotorietyType.Invulnerable ||
            target.IsDead ||
            !_mobiles.IsInWorld(target.Id))
        {
            return false;
        }

        // A ghost, or one gone to another map, is to blame for nothing.
        if (attacker is not null && (attacker.IsDead || attacker.Map != target.Map))
        {
            attacker = null;
        }

        // One's own pet is no innocent to the blast that catches it, and it does not turn on its master.
        var ownPet = IsOwnPet(attacker, target);

        if (attacker is not null)
        {
            Accuse(attacker, target, ownPet);
        }

        Wound(attacker, target, damage, _time.GetUtcNow(), !ownPet);

        return true;
    }

    public bool Aggress(MobileEntity attacker, MobileEntity target)
    {
        if (target.Notoriety == NotorietyType.Invulnerable ||
            target.IsDead ||
            attacker.IsDead ||
            attacker.Map != target.Map ||
            !_mobiles.IsInWorld(target.Id) ||
            !_mobiles.IsInWorld(attacker.Id))
        {
            return false;
        }

        var ownPet = IsOwnPet(attacker, target);
        Accuse(attacker, target, ownPet);

        if (!ownPet && attacker.Id != target.Id)
        {
            FightBack(target, attacker, _time.GetUtcNow());
        }

        return true;
    }

    public void Stop(MobileEntity mobile)
    {
        if (_fighters.Remove(mobile.Id))
        {
            Tell(mobile, Serial.Zero);
        }
    }

    public int RangeOf(MobileEntity mobile)
    {
        return RangedOf(mobile)?.Range ?? _config.MaxRange;
    }

    public WeaponInfo? HeldWeaponOf(MobileEntity mobile)
    {
        return RangedOf(mobile) ?? WeaponOf(mobile);
    }

    public void PlaySwing(MobileEntity mobile, int x, int y)
    {
        var direction = mobile.Location.GetDirectionTo(new Point3D(x, y, mobile.Location.Z)) & (DirectionType)0x07;

        if (!mobile.Frozen && (mobile.Location.X != x || mobile.Location.Y != y) && direction != mobile.Direction)
        {
            mobile.Direction = direction;

            // Its own client too: the movement packet carries the facing, and the one that walks next steps from it.
            _view.MobileFlagsChanged(mobile);
        }

        var (action, frames) = SwingAnimation(mobile, HeldWeaponOf(mobile));
        _view.MobileAnimated(mobile, action, frames, 1);
    }

    public bool SpendAmmo(MobileEntity shooter)
    {
        return RangedOf(shooter) is { } weapon && _ammo is not null && _ammo.Spend(shooter, weapon);
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

    // Why the attacker cannot attack the target, or null when it can.
    private string? Refusal(MobileEntity attacker, MobileEntity target)
    {
        if (attacker.Id == target.Id)
        {
            return "it is itself";
        }

        if (target.Notoriety == NotorietyType.Invulnerable)
        {
            return "the target is invulnerable";
        }

        if (attacker.IsDead)
        {
            return "the attacker is dead";
        }

        if (target.Hits <= 0 || target.IsDead)
        {
            return "the target has no hit points";
        }

        if (!_mobiles.IsInWorld(attacker.Id) || !_mobiles.IsInWorld(target.Id))
        {
            return "one of them is not in the world";
        }

        if (attacker.Map != target.Map)
        {
            return "they are on different maps";
        }

        if (target.IsHiddenFrom(attacker.Id, AccountOf(attacker)))
        {
            return "the target is hidden";
        }

        if (!attacker.Location.InRange(target.Location, _world.ViewRange))
        {
            return "the target is out of view";
        }

        try
        {
            return _sight.HasLineOfSight(attacker.Map, EyeOf(attacker), EyeOf(target)) ? null : "the target is out of sight";
        }
        catch (KeyNotFoundException)
        {
            // A map that is not loaded cannot be fought on.
            return "the map is not loaded";
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
                    _logger.Error(
                        exception,
                        "The fight of {Attacker} against {Target} failed",
                        fighter.Attacker,
                        fighter.Target
                    );
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
            attacker.IsDead ||
            target.IsDead ||
            !attacker.IsNpc && !attacker.WarMode)
        {
            Stop(attacker);

            return;
        }

        if (!InReach(attacker, target) || now < fighter.NextSwingAt)
        {
            return;
        }

        // An archer shoots only what it sees: it keeps the fight, and its script walks it to a place where it does.
        if (RangedOf(attacker) is not null && !CanSee(attacker, target))
        {
            return;
        }

        // A player draws its bow when it has stood still for a while, as ModernUO and UOX3 ask.
        if (!attacker.IsNpc && RangedOf(attacker) is not null && MovedRecently(attacker, now))
        {
            return;
        }

        Swing(fighter, now);
    }

    private bool InReach(MobileEntity attacker, MobileEntity target)
    {
        // A square range, as the scripts and the line of sight measure: a diagonal neighbour is one cell away, not 1.4.
        var range = RangedOf(attacker)?.Range ?? _config.MaxRange;

        return Math.Max(
                   Math.Abs(attacker.Location.X - target.Location.X),
                   Math.Abs(attacker.Location.Y - target.Location.Y)
               ) <= range &&
               Math.Abs(attacker.Location.Z - target.Location.Z) <= ReachInHeight;
    }

    private void Swing(Fighter fighter, DateTimeOffset now)
    {
        var (attacker, target) = (fighter.Attacker, fighter.Target);

        // A player fights with what it holds; an NPC with its template, whatever it is dressed in, except that a bow or a
        // crossbow it holds shoots.
        var weapon = RangedOf(attacker) ?? WeaponOf(attacker);
        fighter.NextSwingAt = now.AddSeconds(
            CombatFormulas.SwingDelaySeconds(attacker.Stamina, weapon?.Speed ?? FistsSpeed, _config.GlobalAttackSpeed)
        );

        // A player spends an arrow or a bolt at each shot; with none the swing is lost, its delay paid, and nothing flies:
        // the player is not shown if it hid, and the fight does not last for it.
        var shot = RangedOf(attacker);

        if (shot is not null && !attacker.IsNpc && _ammo is not null && !_ammo.Spend(attacker, shot))
        {
            return;
        }

        fighter.ExpiresAt = now.AddSeconds(_config.CombatantSeconds);

        // Swinging shows who hid.
        if (attacker.Hidden)
        {
            _state.SetHidden(attacker, false);
        }

        PayStamina(attacker);

        if (_sessions.TryGetByCharacterId(attacker.Id, out var own))
        {
            _sender.TrySend(own.SessionId, new SwingPacket(attacker.Id, target.Id));
        }

        var (action, frames) = SwingAnimation(attacker, weapon);
        _view.MobileAnimated(attacker, action, frames, 1);

        // The arrow or the bolt flies to its target, hit or missed.
        if (weapon is { Type: { } kind } && kind.Projectile != 0)
        {
            _effects?.PlayMoving(
                attacker.Map,
                attacker.Id,
                attacker.Location,
                target.Id,
                target.Location,
                new EffectOptions { Graphic = kind.Projectile, Speed = ProjectileSpeed }
            );

            // Some arrows are found again on the ground where they fell.
            if (!attacker.IsNpc && weapon is not null)
            {
                _ammo?.Recover(target, weapon);
            }
        }

        var attackSkill = weapon?.Skill ?? SkillType.Wrestling;
        var defenseSkill = (RangedOf(target) ?? WeaponOf(target))?.Skill ?? SkillType.Wrestling;
        var chance = CombatFormulas.HitChance(Points(attacker, attackSkill), Points(target, defenseSkill));

        if (!_skills.CheckChance(attacker, attackSkill, chance))
        {
            _speech.PlaySound(attacker, WeaponFamilies.MissSound(weapon?.Type));
            FightBack(target, attacker, now);

            return;
        }

        _speech.PlaySound(
            attacker,
            SoundsOf(attacker)?.Attack is { } sound and > 0 ? sound : WeaponFamilies.HitSound(weapon?.Type)
        );
        Hit(attacker, target, now, weapon);
    }

    private void PayStamina(MobileEntity attacker)
    {
        if (attacker.IsNpc || _config.AttackStamina == 0)
        {
            return;
        }

        _state.SetStats(attacker, new MobileStatsChange { Stamina = Math.Max(attacker.Stamina - _config.AttackStamina, 0) });
    }

    private void Hit(MobileEntity attacker, MobileEntity target, DateTimeOffset now, WeaponInfo? weapon)
    {
        // An invulnerable takes no harm, from a blow it was not refused: a monster's answer to a guard's arrow.
        if (target.Notoriety == NotorietyType.Invulnerable)
        {
            return;
        }

        if (Wound(attacker, target, DamageOf(attacker, target, weapon), now))
        {
            Stop(attacker);
        }
    }

    private static bool IsOwnPet(MobileEntity? attacker, MobileEntity target)
    {
        return attacker is not null && target.GetProp(MountProps.Owner, 0L) == attacker.Id.Value;
    }

    // As an attack: a player who harms an innocent that is not fighting it is a criminal; not for harming itself.
    private void Accuse(MobileEntity attacker, MobileEntity target, bool ownPet)
    {
        if (attacker is { IsNpc: false } &&
            !ownPet &&
            attacker.Id != target.Id &&
            target.ShownNotoriety == NotorietyType.Innocent &&
            TargetOf(target)?.Id != attacker.Id &&
            TargetOf(attacker)?.Id != target.Id)
        {
            _crimes.MakeCriminal(attacker);
            _murders?.Aggressed(attacker, target);
        }
    }

    // What a blow does once it lands: the hurt sound and gesture, the damage, and a death; true when it killed.
    private bool Wound(MobileEntity? attacker, MobileEntity target, int damage, DateTimeOffset now, bool fightBack = true)
    {
        if (SoundsOf(target)?.Hurt is { } hurt and > 0)
        {
            _speech.PlaySound(target, hurt);
        }

        var (action, frames) = HurtAnimation(target);
        _view.MobileAnimated(target, action, frames, 1);
        ShowDamage(attacker, target, damage);

        if (damage > 0)
        {
            _blood?.Splash(target);
        }

        if (attacker is not null)
        {
            _murders?.Struck(attacker, target);
        }

        var hits = target.Hits - damage;

        if (hits > 0)
        {
            _state.SetStats(target, new MobileStatsChange { Hits = hits });

            if (fightBack && attacker is not null && attacker.Id != target.Id)
            {
                FightBack(target, attacker, now);
            }

            return false;
        }

        _state.SetStats(target, new MobileStatsChange { Hits = 0 });

        // A player that cannot die, such as one with a body that has no ghost, is left with one hit point.
        if (!_death.Kill(target, attacker) && !target.IsNpc)
        {
            _state.SetStats(target, new MobileStatsChange { Hits = 1 });
        }

        Stop(target);

        return true;
    }

    // The NPC that is hit, or missed, fights the one who swings, if it fights no one; whoever hit it keeps it at it.
    private void FightBack(MobileEntity victim, MobileEntity attacker, DateTimeOffset now)
    {
        // One that runs, a scared animal or a creature too hurt to fight, does not answer the blow: its script runs. Nor
        // does anyone answer an invulnerable, such as a guard: it cannot be hurt.
        if (!victim.IsNpc ||
            !_mobiles.IsInWorld(victim.Id) ||
            RunsFromBlows(victim) ||
            IsPassive(victim) ||
            attacker.Notoriety == NotorietyType.Invulnerable)
        {
            return;
        }

        if (!_fighters.ContainsKey(victim.Id))
        {
            Fight(victim, attacker, now, false);
        }
    }

    // The bow or the crossbow a mobile holds.
    private WeaponInfo? RangedOf(MobileEntity mobile)
    {
        return _gear.RangedWeaponOf(mobile);
    }

    private bool MovedRecently(MobileEntity mobile, DateTimeOffset now)
    {
        return mobile.LastMovedAt is { } moved && now - moved < TimeSpan.FromSeconds(_config.ArcheryStandStillSeconds);
    }

    private bool CanSee(MobileEntity attacker, MobileEntity target)
    {
        try
        {
            return _sight.HasLineOfSight(attacker.Map, EyeOf(attacker), EyeOf(target));
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    // Where a mobile sees from, as the scripts' sight: the eyes, not the feet, so a fence a mobile sees over does not stop it.
    private static Point3D EyeOf(MobileEntity mobile)
    {
        var spot = mobile.Location;

        return new(spot.X, spot.Y, Math.Min(spot.Z + EyeHeight, sbyte.MaxValue));
    }

    // Whether a script told the NPC not to fight back for now, as a creature that flees.
    private static bool IsPassive(MobileEntity mobile)
    {
        return mobile.TryGetProp<bool>(PassiveProp, out var passive) && passive;
    }

    // Whether the NPC's template names the script of the animals that run instead of fighting back.
    private bool RunsFromBlows(MobileEntity mobile)
    {
        return mobile.TemplateId is { } id &&
               _templates.TryGet(id, out var template) &&
               template.ScriptId == ScaredAnimalScript;
    }

    // The weapon a player holds; none for an NPC, which fights with its template.
    private WeaponInfo? WeaponOf(MobileEntity mobile)
    {
        return mobile.IsNpc ? null : _gear.WeaponOf(mobile);
    }

    private int DamageOf(MobileEntity attacker, MobileEntity target, WeaponInfo? weapon)
    {
        var damage = Math.Max(BaseDamage(attacker, weapon), CombatFormulas.FistsMinimumDamage);
        // Tactics and anatomy are tried at every hit: they teach a player as they are used.
        _skills.Check(attacker, SkillType.Tactics, PassiveMinimum, PassiveMaximum);
        _skills.Check(attacker, SkillType.Anatomy, PassiveMinimum, PassiveMaximum);
        damage = CombatFormulas.ScaleDamage(
            damage,
            Points(attacker, SkillType.Tactics),
            attacker.EffectiveStrength,
            Points(attacker, SkillType.Anatomy),
            // One who fells trees hits harder with an axe; the skill is not tried, it grows on trees.
            weapon?.Type == WeaponType.Axe ? Points(attacker, SkillType.Lumberjacking) : 0,
            weapon?.Quality ?? ItemQualityType.Regular
        );
        // As ModernUO's classic: a player hit, or a hit by an NPC, does half; a player hitting an NPC does all.
        var halved = !target.IsNpc || attacker.IsNpc;
        var rate = attacker.IsNpc && !target.IsNpc ? _config.NpcDamageRate : 1.0;

        return Absorb(CombatFormulas.Reduce(damage, halved, rate), target);
    }

    // An NPC has the armor of its template, one number; a player the piece of armor on the part the blow lands on.
    private int Absorb(int damage, MobileEntity target)
    {
        if (target.IsNpc)
        {
            return CombatFormulas.Final(damage, target.Armor, _random);
        }

        var piece = _gear.ArmorAt(target, CombatFormulas.ZoneOf(_random.NextDouble()));

        return Math.Max(damage - CombatFormulas.AbsorbedByPiece(piece, _random), 1);
    }

    // The weapon of a player, between its least and its most; the dice of the template of an NPC; else the fists of ModernUO.
    private int BaseDamage(MobileEntity attacker, WeaponInfo? weapon)
    {
        // An NPC hits with the dice of its template, a bow in its hands or not, as ModernUO's creatures do.
        if (attacker.IsNpc &&
            attacker.TemplateId is { } id &&
            _templates.TryGet(id, out var template) &&
            template.Damage is { } dice)
        {
            return dice.Roll();
        }

        if (weapon is not null)
        {
            return weapon.DamageMin + _random.Next(Math.Max(weapon.DamageMax - weapon.DamageMin, 0) + 1);
        }

        return _random.Next(CombatFormulas.FistsMaximumDamage) + CombatFormulas.FistsMinimumDamage;
    }

    private void ShowDamage(MobileEntity? attacker, MobileEntity target, int damage)
    {
        if (!_config.DisplayDamageNumbers)
        {
            return;
        }

        foreach (var player in new[] { attacker, target })
        {
            if (player is not null && !player.IsNpc && _sessions.TryGetByCharacterId(player.Id, out var session))
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
        return mobile.IsNpc && mobile.TemplateId is { } id && _templates.TryGet(id, out var template)
            ? template.Sounds
            : null;
    }

    private BodyType BodyOf(MobileEntity mobile)
    {
        return _bodies.Value.GetValueOrDefault(mobile.Body, BodyType.Monster);
    }

    // A rider swings with the actions of a mount; a walker with the ones of its weapon.
    private HumanAnimationType HumanSwing(MobileEntity attacker, WeaponInfo? weapon)
    {
        return _mounts?.IsMounted(attacker) == true
            ? WeaponFamilies.MountedAction(weapon?.Type, weapon?.TwoHanded == true)
            : WeaponFamilies.Action(weapon?.Type, weapon?.TwoHanded == true);
    }

    private (int Action, int Frames) SwingAnimation(MobileEntity attacker, WeaponInfo? weapon)
    {
        return BodyOf(attacker) switch
        {
            BodyType.Human  => ((int)HumanSwing(attacker, weapon), SwingFrames),
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

    // The points of a skill, whole and tenths, as 50.5; 0 for one the mobile does not have.
    private static double Points(MobileEntity mobile, SkillType skill)
    {
        return (mobile.Skills.FirstOrDefault(known => known.Skill == skill)?.Base ?? 0) / Tenth;
    }
}
