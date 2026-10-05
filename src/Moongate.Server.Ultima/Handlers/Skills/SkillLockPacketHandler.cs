using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Handlers.Skills;

/// <summary>
///     Sets the lock of a skill of the character when its player changes it in the skill window (0x3A). A skill or a
///     lock that does not exist is ignored; nothing is sent back, as the client already shows what it asked for.
/// </summary>
public sealed class SkillLockPacketHandler : IPacketHandler<SkillLockPacket>
{
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;

    public SkillLockPacketHandler(IMobileService mobiles, IMobileStateService state)
    {
        _mobiles = mobiles;
        _state = state;
    }

    public void Handle(GameSession session, SkillLockPacket packet)
    {
        if (!session.CharacterId.IsValid ||
            !_mobiles.TryGet(session.CharacterId, out var character) ||
            !_mobiles.IsInWorld(character.Id))
        {
            return;
        }

        _state.SetSkillLock(character, (SkillType)packet.Skill, (SkillLockType)packet.Lock);
    }
}
