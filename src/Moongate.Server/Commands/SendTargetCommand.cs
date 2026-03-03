using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Sends a target cursor to the caller.
/// </summary>
[RegisterConsoleCommand(
    "send_target",
    "Sends a target cursor to the specified player. Usage: send_target",
    CommandSourceType.InGame,
    AccountType.Regular
)]
public sealed class SendTargetCommand : ICommandExecutor
{
    private readonly IGameEventBusService _gameEventBusService;

    public SendTargetCommand(IGameEventBusService gameEventBusService)
    {
        _gameEventBusService = gameEventBusService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        await _gameEventBusService.PublishAsync(
            new TargetRequestCursorEvent(
                context.SessionId,
                TargetCursorSelectionType.SelectLocation,
                TargetCursorType.Helpful,
                callback =>
                {
                    context.Print(
                        "Target cursor callback invoked with selection: {0}",
                        callback.Packet.Location
                    );
                }
            )
        );
    }
}
