using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Answers the client's mobile query (0x34): the status of the character itself, all of it, or of a mobile it
///     sees, as its name and health bar; and the character's skills, when the skill window opens. Anything else is
///     left unanswered.
/// </summary>
public sealed class MobileQueryPacketHandler : IPacketHandler<MobileQueryPacket>
{
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly WorldConfig _world;

    public MobileQueryPacketHandler(IMobileService mobiles, IMobileStateService state, WorldConfig world)
    {
        _mobiles = mobiles;
        _state = state;
        _world = world;
    }

    public void Handle(GameSession session, MobileQueryPacket packet)
    {
        if (!session.CharacterId.IsValid || !_mobiles.TryGet(session.CharacterId, out var character))
        {
            return;
        }

        switch (packet.Kind)
        {
            case MobileQueryType.Skills:
                _state.SendSkills(session, character);

                break;
            case MobileQueryType.Status when _mobiles.TryGet(packet.Target, out var target) &&
                                             target.Map == character.Map &&
                                             Math.Abs(target.Location.X - character.Location.X) <= _world.ViewRange &&
                                             Math.Abs(target.Location.Y - character.Location.Y) <= _world.ViewRange:
                _state.SendStatus(session, target);

                break;
        }
    }
}
