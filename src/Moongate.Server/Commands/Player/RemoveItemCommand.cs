using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

/// <summary>
/// Removes a targeted item or NPC from the world.
/// </summary>
[RegisterConsoleCommand(
    "remove_item|.remove_item",
    "Target an item or NPC and remove it. Usage: .remove_item",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class RemoveItemCommand : ICommandExecutor
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IPlayerTargetService _playerTargetService;
    private readonly IItemService _itemService;
    private readonly IMobileService _mobileService;
    private readonly ISpatialWorldService _spatialWorldService;

    public RemoveItemCommand(
        IGameNetworkSessionService gameNetworkSessionService,
        IPlayerTargetService playerTargetService,
        IItemService itemService,
        IMobileService mobileService,
        ISpatialWorldService spatialWorldService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _playerTargetService = playerTargetService;
        _itemService = itemService;
        _mobileService = mobileService;
        _spatialWorldService = spatialWorldService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.Print("Usage: .remove_item");

            return;
        }

        if (!_gameNetworkSessionService.TryGet(context.SessionId, out _))
        {
            context.PrintError("No active session found for remove_item.");

            return;
        }

        await _playerTargetService.SendTargetCursorAsync(
            context.SessionId,
            callback =>
            {
                try
                {
                    HandleTargetSelection(context, callback);
                }
                catch (Exception exception)
                {
                    context.PrintError("Failed to remove target: {0}", exception.Message);
                }
            },
            TargetCursorSelectionType.SelectObject,
            TargetCursorType.Helpful
        );
    }

    private void HandleTargetSelection(CommandSystemContext context, PendingCursorCallback callback)
    {
        var targetSerial = callback.Packet.ClickedOnId;

        if (targetSerial == Serial.Zero)
        {
            context.PrintError("Target is not valid.");

            return;
        }

        if (targetSerial.IsItem)
        {
            RemoveItem(context, targetSerial);

            return;
        }

        if (targetSerial.IsMobile)
        {
            RemoveMobile(context, targetSerial);

            return;
        }

        context.PrintError("Target is not a valid item or mobile.");
    }

    private void RemoveItem(CommandSystemContext context, Serial targetSerial)
    {
        var item = _itemService.GetItemAsync(targetSerial).GetAwaiter().GetResult();

        if (item is null)
        {
            context.PrintError("Target item not found.");

            return;
        }

        var removed = _itemService.DeleteItemAsync(targetSerial).GetAwaiter().GetResult();

        if (!removed)
        {
            context.PrintError("Failed to remove item.");

            return;
        }

        context.Print("Removed item {0}.", ResolveItemName(item));
    }

    private void RemoveMobile(CommandSystemContext context, Serial targetSerial)
    {
        var target = ResolveTargetMobile(targetSerial);

        if (target is null)
        {
            context.PrintError("Target mobile not found.");

            return;
        }

        if (target.IsPlayer)
        {
            context.PrintError("Cannot remove player characters.");

            return;
        }

        var removed = _mobileService.DeleteAsync(targetSerial).GetAwaiter().GetResult();

        if (!removed)
        {
            context.PrintError("Failed to remove NPC.");

            return;
        }

        _spatialWorldService.RemoveEntity(targetSerial);
        context.Print("Removed NPC {0}.", ResolveMobileName(target));
    }

    private static string ResolveItemName(UOItemEntity item)
        => string.IsNullOrWhiteSpace(item.Name) ? item.Id.ToString() : item.Name;

    private static string ResolveMobileName(UOMobileEntity target)
        => string.IsNullOrWhiteSpace(target.Name) ? target.Id.ToString() : target.Name;

    private UOMobileEntity? ResolveTargetMobile(Serial targetSerial)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(targetSerial, out var session) && session.Character is not null)
        {
            return session.Character;
        }

        return _mobileService.GetAsync(targetSerial).GetAwaiter().GetResult();
    }
}
