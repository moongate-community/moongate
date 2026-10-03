using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
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
            SendStatus(own, mobile);
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

        known.Cap = cap ?? known.Cap;
        known.Base = Math.Clamp(value, 0, known.Cap);

        if (_sessions.TryGetByCharacterId(mobile.Id, out var own))
        {
            _sender.TrySend(own.SessionId, SkillsPacket.One(known));
        }

        return true;
    }

    public bool SetName(MobileEntity mobile, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        mobile.Name = name.Trim();
        Shown(mobile);

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
        Shown(mobile);

        return true;
    }

    public void SendStatus(GameSession session, MobileEntity target)
    {
        _sender.TrySend(session.SessionId, new MobileStatusPacket(_mobiles.GetStatus(target), session.CharacterId != target.Id));
    }

    public void SendSkills(GameSession session, MobileEntity character)
    {
        _sender.TrySend(session.SessionId, SkillsPacket.All(GetSkills(character)));
    }

    // Its own player gets its status and its figure again; the players around its figure.
    private void Shown(MobileEntity mobile)
    {
        if (!_mobiles.IsInWorld(mobile.Id))
        {
            return;
        }

        if (_sessions.TryGetByCharacterId(mobile.Id, out var own))
        {
            SendStatus(own, mobile);
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
    }

    // The sessions of the players who see the mobile, its own left out.
    private IEnumerable<GameSession> Around(MobileEntity mobile)
    {
        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, _world.ViewRange))
        {
            if (other.Id != mobile.Id && _sessions.TryGetByCharacterId(other.Id, out var session))
            {
                yield return session;
            }
        }
    }
}
