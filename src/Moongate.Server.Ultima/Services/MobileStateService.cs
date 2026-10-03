using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Changes the numbers, the skills, the name and the looks of a live mobile and sends what the clients show of
///     them, as ModernUO's deltas: the whole status to the mobile's own player, the health bar as a share of 100 to the
///     players around, one skill to the skill window, the figure again to those who see it.
/// </summary>
public sealed class MobileStateService : IMobileStateService
{
    private const int DefaultSkillCap = 1000;

    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly ISectorService _sectors;
    private readonly IPacketSendService _sender;
    private readonly IWorldViewService _view;
    private readonly WorldConfig _world;

    public MobileStateService(
        IMobileService mobiles,
        ISessionService sessions,
        ISectorService sectors,
        IPacketSendService sender,
        IWorldViewService view,
        WorldConfig world
    )
    {
        _mobiles = mobiles;
        _sessions = sessions;
        _sectors = sectors;
        _sender = sender;
        _view = view;
        _world = world;
    }

    public bool SetStats(MobileEntity mobile, MobileStatsChange change)
    {
        if (new[] { change.Strength, change.Dexterity, change.Intelligence, change.HitsMax, change.ManaMax, change.StaminaMax }
            .Any(value => value is < 0 or > IMobileStateService.MaxValue))
        {
            return false;
        }

        var health = (mobile.Hits, mobile.HitsMax);
        var mana = (mobile.Mana, mobile.ManaMax);
        var stamina = (mobile.Stamina, mobile.StaminaMax);
        // What only the whole status shows.
        var others = (mobile.Strength, mobile.Dexterity, mobile.Intelligence, mobile.Fame, mobile.Karma);
        mobile.Strength = change.Strength ?? mobile.Strength;
        mobile.Dexterity = change.Dexterity ?? mobile.Dexterity;
        mobile.Intelligence = change.Intelligence ?? mobile.Intelligence;
        mobile.HitsMax = change.HitsMax ?? mobile.HitsMax;
        mobile.ManaMax = change.ManaMax ?? mobile.ManaMax;
        mobile.StaminaMax = change.StaminaMax ?? mobile.StaminaMax;
        // A lowered maximum takes what is above it.
        mobile.Hits = Math.Clamp(change.Hits ?? mobile.Hits, 0, mobile.HitsMax);
        mobile.Mana = Math.Clamp(change.Mana ?? mobile.Mana, 0, mobile.ManaMax);
        mobile.Stamina = Math.Clamp(change.Stamina ?? mobile.Stamina, 0, mobile.StaminaMax);
        mobile.Fame = change.Fame ?? mobile.Fame;
        mobile.Karma = change.Karma ?? mobile.Karma;

        if (!_mobiles.IsInWorld(mobile.Id))
        {
            return true;
        }

        if (_sessions.TryGetByCharacterId(mobile.Id, out var own))
        {
            // As ModernUO: the bar that moved, or the whole status when a stat did.
            if (others != (mobile.Strength, mobile.Dexterity, mobile.Intelligence, mobile.Fame, mobile.Karma))
            {
                SendStatus(own, mobile);
            }
            else
            {
                if (health != (mobile.Hits, mobile.HitsMax))
                {
                    _sender.TrySend(own.SessionId, new MobileHitsPacket(mobile.Id, mobile.Hits, mobile.HitsMax));
                }

                if (mana != (mobile.Mana, mobile.ManaMax))
                {
                    _sender.TrySend(own.SessionId, new MobileManaPacket(mobile.Id, mobile.Mana, mobile.ManaMax));
                }

                if (stamina != (mobile.Stamina, mobile.StaminaMax))
                {
                    _sender.TrySend(own.SessionId, new MobileStaminaPacket(mobile.Id, mobile.Stamina, mobile.StaminaMax));
                }
            }
        }

        if (health != (mobile.Hits, mobile.HitsMax))
        {
            // Never the real numbers: a share of 100.
            var bar = new MobileHitsPacket(mobile.Id, mobile.Hits, mobile.HitsMax, true);

            foreach (var session in Around(mobile))
            {
                _sender.TrySend(session.SessionId, bar);
            }
        }

        return true;
    }

    public MobileSkill GetSkill(MobileEntity mobile, SkillType skill)
    {
        return mobile.Skills.FirstOrDefault(known => known.Skill == skill) ?? new MobileSkill { Skill = skill };
    }

    public IReadOnlyList<MobileSkill> GetSkills(MobileEntity mobile)
    {
        return Enum.GetValues<SkillType>().Select(skill => GetSkill(mobile, skill)).ToList();
    }

    public bool SetSkill(MobileEntity mobile, SkillType skill, int value, int? cap = null)
    {
        if (!Enum.IsDefined(skill) || cap is < 0 or > IMobileStateService.MaxValue)
        {
            return false;
        }

        var known = mobile.Skills.FirstOrDefault(entry => entry.Skill == skill);

        if (known is null)
        {
            known = new() { Skill = skill, Cap = DefaultSkillCap };
            mobile.Skills.Add(known);
        }

        var before = (known.Base, known.Cap);
        known.Cap = cap ?? known.Cap;
        known.Base = Math.Clamp(value, 0, known.Cap);

        if (before != (known.Base, known.Cap) && _sessions.TryGetByCharacterId(mobile.Id, out var own))
        {
            _sender.TrySend(own.SessionId, SkillsPacket.One(known));
        }

        return true;
    }

    public bool SetName(MobileEntity mobile, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > IMobileStateService.MaxNameLength)
        {
            return false;
        }

        mobile.Name = name.Trim();

        if (!_mobiles.IsInWorld(mobile.Id))
        {
            return true;
        }

        // The status carries the name; the figure is the same, so the client is not placed again.
        if (_sessions.TryGetByCharacterId(mobile.Id, out var own))
        {
            SendStatus(own, mobile);
        }

        _view.MobileAppeared(mobile);

        return true;
    }

    public bool SetLooks(MobileEntity mobile, int? body, int? hue)
    {
        if (body is < 0 or > ushort.MaxValue || hue is < 0 or > ushort.MaxValue)
        {
            return false;
        }

        mobile.Body = body ?? mobile.Body;
        mobile.SkinHue = hue is { } skin ? new Hue((ushort)skin) : mobile.SkinHue;

        if (!_mobiles.IsInWorld(mobile.Id))
        {
            return true;
        }

        if (_sessions.TryGetByCharacterId(mobile.Id, out var own))
        {
            // The client starts its step sequence again when it gets 0x20; a step it sent before is refused, and the
            // next one is due at once, as after a teleport.
            if (own.Get(MovementSessionKeys.State) is { } steps)
            {
                steps.ExpectedSequence = 0;
                steps.NextStepAt = 0;
            }

            _sender.TrySend(
                own.SessionId,
                new MobileUpdatePacket(
                    mobile.Id,
                    new Body((ushort)mobile.Body),
                    mobile.SkinHue,
                    _mobiles.GetFlags(mobile),
                    mobile.Location,
                    mobile.Direction
                )
            );
        }

        _view.MobileAppeared(mobile);

        return true;
    }

    public void SetHidden(MobileEntity mobile, bool hidden)
    {
        if (mobile.Hidden == hidden)
        {
            return;
        }

        mobile.Hidden = hidden;

        if (_mobiles.IsInWorld(mobile.Id))
        {
            _view.MobileHiddenChanged(mobile);
        }
    }

    public void SetFrozen(MobileEntity mobile, bool frozen)
    {
        if (mobile.Frozen == frozen)
        {
            return;
        }

        mobile.Frozen = frozen;

        if (_mobiles.IsInWorld(mobile.Id))
        {
            _view.MobileFlagsChanged(mobile);
        }
    }

    public void SetWarMode(MobileEntity mobile, bool warMode)
    {
        var changed = mobile.WarMode != warMode;
        mobile.WarMode = warMode;

        if (!_mobiles.IsInWorld(mobile.Id))
        {
            return;
        }

        // The client waits for the answer to its own request, whatever the mode was.
        if (_sessions.TryGetByCharacterId(mobile.Id, out var own))
        {
            _sender.TrySend(own.SessionId, new WarModePacket(warMode));
        }

        if (changed)
        {
            _view.MobileFlagsChanged(mobile);
        }
    }

    public void SendStatus(GameSession session, MobileEntity target)
    {
        if (!CanSee(session, target))
        {
            return;
        }

        _sender.TrySend(session.SessionId, new MobileStatusPacket(_mobiles.GetStatus(target), session.CharacterId != target.Id));
    }

    public void SendSkills(GameSession session, MobileEntity character)
    {
        _sender.TrySend(session.SessionId, SkillsPacket.All(GetSkills(character)));
    }

    // The sessions of the players who see the mobile, its own left out.
    private IEnumerable<GameSession> Around(MobileEntity mobile)
    {
        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, _world.ViewRange))
        {
            if (other.Id != mobile.Id && _sessions.TryGetByCharacterId(other.Id, out var session) && CanSee(session, mobile))
            {
                yield return session;
            }
        }
    }

    // A hidden mobile is seen by its own player and by the staff.
    private static bool CanSee(GameSession session, MobileEntity mobile)
    {
        return !mobile.Hidden || session.CharacterId == mobile.Id || session.AccountType >= AccountType.GameMaster;
    }
}
