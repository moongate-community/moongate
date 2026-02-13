using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Packets.Mouse;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class TargetCursorHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<TargetCursorHandler>();

    private readonly ICallbackService _callbackService;

    public TargetCursorHandler(ICallbackService callbackService)
        => _callbackService = callbackService;

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is TargetCursorPacket targetCursorPacket)
        {
            await ProcessTargetCursorAsync(session, targetCursorPacket);
        }
    }

    private Task ProcessTargetCursorAsync(GameSession session, TargetCursorPacket packet)
    {
        var result = _callbackService.ExecuteCallback(
            packet.CursorId,
            packet.SelectionType,
            packet.ClickedPoint,
            packet.ClickedSerial
        );

        if (!result)
        {
            _logger.Warning(
                "No callback found for cursor ID {CursorId} with selection type {SelectionType}",
                packet.CursorId,
                packet.SelectionType
            );
        }

        return Task.CompletedTask;
    }
}
