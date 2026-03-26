using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "lock_door",
    "Target a door, assign a lock id, and create a matching key in your backpack.",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class LockDoorCommand : ICommandExecutor
{
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IDoorLockService _doorLockService;
    private readonly IItemService _itemService;

    public LockDoorCommand(
        IGameEventBusService gameEventBusService,
        IGameNetworkSessionService gameNetworkSessionService,
        IDoorLockService doorLockService,
        IItemService itemService
    )
    {
        _gameEventBusService = gameEventBusService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _doorLockService = doorLockService;
        _itemService = itemService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (!_gameNetworkSessionService.TryGet(context.SessionId, out var session) || session.Character is null)
        {
            context.PrintError("No active character found for lock_door.");

            return;
        }

        var backpackId = ResolveBackpackId(session.Character);

        if (backpackId == Serial.Zero)
        {
            context.PrintError("Backpack not found.");

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
                        HandleTargetSelection(context, callback.Packet.ClickedOnId, backpackId);
                    }
                    catch (Exception exception)
                    {
                        context.PrintError("Failed to lock door: {0}", exception.Message);
                    }
                }
            )
        );
    }

    private static string BuildKeyName(UOItemEntity? door)
    {
        if (door is null || string.IsNullOrWhiteSpace(door.Name))
        {
            return "Key";
        }

        return $"{door.Name}'s key";
    }

    private void HandleTargetSelection(CommandSystemContext context, Serial targetSerial, Serial backpackId)
    {
        if (targetSerial == Serial.Zero || !targetSerial.IsItem)
        {
            context.PrintError("Target is not a valid door.");

            return;
        }

        var result = _doorLockService.LockDoorAsync(targetSerial).GetAwaiter().GetResult();

        if (!result.Locked || string.IsNullOrWhiteSpace(result.LockId))
        {
            context.PrintError("Target is not a valid door.");

            return;
        }

        var door = _itemService.GetItemAsync(targetSerial).GetAwaiter().GetResult();
        var key = _itemService.SpawnFromTemplateAsync("key").GetAwaiter().GetResult();
        key.SetCustomString(ItemCustomParamKeys.Key.LockId, result.LockId);
        key.Name = BuildKeyName(door);
        _itemService.UpsertItemAsync(key).GetAwaiter().GetResult();
        _itemService.MoveItemToContainerAsync(key.Id, backpackId, new(1, 1), context.SessionId).GetAwaiter().GetResult();

        context.Print("Door locked. Key created with lock id {0}.", result.LockId);
    }

    private static Serial ResolveBackpackId(UOMobileEntity character)
    {
        if (character.BackpackId != Serial.Zero)
        {
            return character.BackpackId;
        }

        if (character.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId))
        {
            return equippedBackpackId;
        }

        return Serial.Zero;
    }
}
