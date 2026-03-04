using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.UI;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.GumpMenuSelectionPacket)]

/// <summary>
/// Handles incoming gump button response packets (0xB1).
/// </summary>
public sealed class GumpHandler : BasePacketListener
{
    private readonly IGumpScriptDispatcherService _gumpScriptDispatcherService;

    public GumpHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IGumpScriptDispatcherService gumpScriptDispatcherService
    )
        : base(outgoingPacketQueue)
    {
        _gumpScriptDispatcherService = gumpScriptDispatcherService;
    }

    protected override Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is GumpMenuSelectionPacket gumpPacket)
        {
            _gumpScriptDispatcherService.TryDispatch(session, gumpPacket);
        }

        return Task.FromResult(true);
    }
}

