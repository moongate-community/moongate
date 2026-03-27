using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

/// <summary>
/// Flips a targeted item if it defines flippable item ids.
/// </summary>
[RegisterConsoleCommand(
    "flip",
    "Target an item and flip it to the next configured itemId.",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class FlipCommand : ICommandExecutor
{
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService _spatialWorldService;

    public FlipCommand(
        IGameEventBusService gameEventBusService,
        IItemService itemService,
        ISpatialWorldService spatialWorldService
    )
    {
        _gameEventBusService = gameEventBusService;
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.Print("Usage: .flip");

            return;
        }

        await _gameEventBusService.PublishAsync(
            new TargetRequestCursorEvent(
                context.SessionId,
                TargetCursorSelectionType.SelectObject,
                TargetCursorType.Helpful,
                callback =>
                {
                    try
                    {
                        var targetSerial = callback.Packet.ClickedOnId;

                        if (targetSerial == Serial.Zero || !targetSerial.IsItem)
                        {
                            context.PrintError("Selected target is not a valid item.");

                            return;
                        }

                        var (found, item) = _itemService.TryToGetItemAsync(targetSerial).GetAwaiter().GetResult();

                        if (!found || item is null)
                        {
                            context.PrintError("Selected target is not a valid item.");

                            return;
                        }

                        var itemProxy = new LuaItemProxy(item, _itemService, _spatialWorldService);

                        if (!itemProxy.Flip())
                        {
                            context.PrintError(
                                "Item {0} does not define flippable variants.",
                                item.Id
                            );

                            return;
                        }

                        context.Print("Item {0} flipped to 0x{1:X4}.", item.Id, item.ItemId);
                    }
                    catch (Exception exception)
                    {
                        context.PrintError("Failed to flip item: {0}", exception.Message);
                    }
                }
            )
        );
    }
}
