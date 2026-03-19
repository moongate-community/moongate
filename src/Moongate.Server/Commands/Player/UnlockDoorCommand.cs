using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "unlock_door|.unlock_door",
    "Target a locked door and remove its lock id.",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class UnlockDoorCommand : ICommandExecutor
{
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IDoorLockService _doorLockService;

    public UnlockDoorCommand(
        IGameEventBusService gameEventBusService,
        IGameNetworkSessionService gameNetworkSessionService,
        IDoorLockService doorLockService
    )
    {
        _gameEventBusService = gameEventBusService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _doorLockService = doorLockService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (!_gameNetworkSessionService.TryGet(context.SessionId, out _))
        {
            context.PrintError("No active session found for unlock_door.");

            return;
        }

        await _gameEventBusService.PublishAsync(
            new TargetRequestCursorEvent(
                context.SessionId,
                TargetCursorSelectionType.SelectObject,
                TargetCursorType.Neutral,
                callback =>
                {
                    try
                    {
                        HandleTargetSelection(context, callback.Packet.ClickedOnId);
                    }
                    catch (Exception exception)
                    {
                        context.PrintError("Failed to unlock door: {0}", exception.Message);
                    }
                }
            )
        );
    }

    private void HandleTargetSelection(CommandSystemContext context, Serial targetSerial)
    {
        if (targetSerial == Serial.Zero || !targetSerial.IsItem)
        {
            context.PrintError("Target is not a valid door.");

            return;
        }

        var unlocked = _doorLockService.UnlockDoorAsync(targetSerial).GetAwaiter().GetResult();

        if (!unlocked)
        {
            context.PrintError("Target is not a valid door.");

            return;
        }

        context.Print("Door unlocked.");
    }
}
