using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Combat;
using Moongate.Server.Ultima.Packets.General;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Combat;

/// <summary>
///     Makes the character of a player fight the mobile it clicked (0x05). A target that is not there, or that the
///     combat service refuses, is answered with a clear target (0xAA with zero), as the other emulators do, so the
///     client does not keep it highlighted.
/// </summary>
public sealed class AttackRequestPacketHandler : IPacketHandler<AttackRequestPacket>
{
    private readonly ILogger _logger = Log.ForContext<AttackRequestPacketHandler>();
    private readonly IMobileService _mobiles;
    private readonly ICombatService _combat;
    private readonly IPacketSendService _sender;

    public AttackRequestPacketHandler(IMobileService mobiles, ICombatService combat, IPacketSendService sender)
    {
        _mobiles = mobiles;
        _combat = combat;
        _sender = sender;
    }

    public void Handle(GameSession session, AttackRequestPacket packet)
    {
        if (!session.CharacterId.IsValid ||
            !_mobiles.TryGet(session.CharacterId, out var character) ||
            !_mobiles.IsInWorld(character.Id))
        {
            return;
        }

        if (!_mobiles.TryGet(packet.Target, out var target))
        {
            _logger.Information("{Character} asked to attack {Target}, which is not in the world", character, packet.Target);
        }
        else if (_combat.Attack(character, target))
        {
            _logger.Debug("{Character} attacks {Target}", character, target);

            return;
        }

        _sender.TrySend(session.SessionId, new CombatantPacket(Serial.Zero));
    }
}
