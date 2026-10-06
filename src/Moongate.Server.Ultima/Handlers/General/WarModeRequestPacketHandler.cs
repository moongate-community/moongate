using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Puts the character in the mode its client asks (0x72), war or peace, and answers with the mode it is in; the
///     players around see the stance change.
/// </summary>
public sealed class WarModeRequestPacketHandler : IPacketHandler<WarModeRequestPacket>
{
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly ICombatService? _combat;

    public WarModeRequestPacketHandler(IMobileService mobiles, IMobileStateService state, ICombatService? combat = null)
    {
        _mobiles = mobiles;
        _state = state;
        _combat = combat;
    }

    public void Handle(GameSession session, WarModeRequestPacket packet)
    {
        if (session.CharacterId.IsValid && _mobiles.TryGet(session.CharacterId, out var character))
        {
            _state.SetWarMode(character, packet.WarMode);

            // As ModernUO: peace ends the fight.
            if (!packet.WarMode)
            {
                _combat?.Stop(character);
            }
        }
    }
}
