using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.RequestAttackPacket)]

/// <summary>
/// Handles player attack-target selection.
/// </summary>
public sealed class RequestAttackHandler : BasePacketListener
{
    private readonly ICombatService _combatService;

    public RequestAttackHandler(IOutgoingPacketQueue outgoingPacketQueue, ICombatService combatService)
        : base(outgoingPacketQueue)
    {
        _combatService = combatService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not RequestAttackPacket requestAttackPacket || session.CharacterId == Serial.Zero)
        {
            return true;
        }

        _ = await _combatService.TrySetCombatantAsync(session.CharacterId, (Serial)requestAttackPacket.TargetId);

        return true;
    }
}
