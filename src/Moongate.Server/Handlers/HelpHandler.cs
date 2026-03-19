using Moongate.Network.Packets.Incoming.UI;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(0x9B)]
public sealed class HelpHandler : BasePacketListener
{
    private readonly IHelpRequestService _helpRequestService;

    public HelpHandler(IOutgoingPacketQueue outgoingPacketQueue, IHelpRequestService helpRequestService)
        : base(outgoingPacketQueue)
    {
        _helpRequestService = helpRequestService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not RequestHelpPacket)
        {
            return false;
        }

        await _helpRequestService.OpenAsync(session);

        return true;
    }
}
