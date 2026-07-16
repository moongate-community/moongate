using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.Server.Interfaces.Network;
using Moongate.UO.Data.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles skill lock change (0x3A): records the up/down/lock arrow the player set on a skill and
/// persists it. Like ModernUO's SetLockNoRelay it does not echo back — the client already moved its own
/// arrow. Locking an untrained skill creates its entry at value zero so the choice survives.
/// </summary>
public sealed class SkillLockChangeHandler : IPacketHandler<SkillLockChangePacket>, IPacketHandlerRegistration
{
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly ISkillService _skills;

    public SkillLockChangeHandler(IPersistenceService persistence, ISkillService skills)
    {
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
        _skills = skills;
    }

    public void Handle(SkillLockChangePacket packet, in PacketContext context)
    {
        if (context.Session.Character is { } mobile && TryApplyLock(mobile, packet.SkillId, packet.Lock, _skills))
        {
            _mobiles.UpsertAsync(mobile).WaitSync();
        }
    }

    /// <summary>
    /// Sets the lock on <paramref name="mobile" />'s skill, creating the entry (at value zero) if the
    /// skill was untrained. Returns false — changing nothing — for a skill id that is not registered,
    /// so a bogus id cannot seed junk entries.
    /// </summary>
    public static bool TryApplyLock(MobileEntity mobile, ushort skillId, SkillLockType skillLock, ISkillService skills)
    {
        if (skills.GetById(skillId) is null)
        {
            return false;
        }

        if (mobile.Skills.TryGetValue(skillId, out var skill))
        {
            skill.Lock = skillLock;
        }
        else
        {
            mobile.Skills[skillId] = new MobileSkill { Lock = skillLock };
        }

        return true;
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
