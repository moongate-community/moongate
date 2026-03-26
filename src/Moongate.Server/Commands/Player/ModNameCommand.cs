using Moongate.Network.Packets.Helpers;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

/// <summary>
/// Renames a targeted item or mobile and refreshes nearby clients.
/// </summary>
[RegisterConsoleCommand(
    "mod_name",
    "Target an item or mobile and replace its display name.",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class ModNameCommand : ICommandExecutor
{
    private const int MaxNameLength = 60;

    private readonly IGameEventBusService _gameEventBusService;
    private readonly IItemService _itemService;
    private readonly IMobileService _mobileService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public ModNameCommand(
        IGameEventBusService gameEventBusService,
        IItemService itemService,
        IMobileService mobileService,
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _gameEventBusService = gameEventBusService;
        _itemService = itemService;
        _mobileService = mobileService;
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        var newName = string.Join(' ', context.Arguments).Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            context.Print("Usage: .mod_name <new name>");

            return;
        }

        if (newName.Length > MaxNameLength)
        {
            context.PrintError("Name cannot be longer than {0} characters.", MaxNameLength);

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
                        HandleTargetSelection(context, newName, callback.Packet.ClickedOnId);
                    }
                    catch (Exception exception)
                    {
                        context.PrintError("Failed to rename target: {0}", exception.Message);
                    }
                }
            )
        );
    }

    private void HandleTargetSelection(CommandSystemContext context, string newName, Serial targetSerial)
    {
        if (targetSerial == Serial.Zero)
        {
            context.PrintError("Selected target is not a valid item or mobile.");

            return;
        }

        if (targetSerial.IsItem)
        {
            var (found, item) = _itemService.TryToGetItemAsync(targetSerial).GetAwaiter().GetResult();

            if (!found || item is null)
            {
                context.PrintError("Selected target is not a valid item or mobile.");

                return;
            }

            RenameItem(context, item, newName);

            return;
        }

        if (targetSerial.IsMobile)
        {
            var mobile = _mobileService.GetAsync(targetSerial).GetAwaiter().GetResult();

            if (mobile is null)
            {
                context.PrintError("Selected target is not a valid item or mobile.");

                return;
            }

            RenameMobile(context, mobile, newName);

            return;
        }

        context.PrintError("Selected target is not a valid item or mobile.");
    }

    private void RenameItem(CommandSystemContext context, UOItemEntity item, string newName)
    {
        item.Name = newName;
        _itemService.UpsertItemAsync(item).GetAwaiter().GetResult();

        if (item.ParentContainerId == Serial.Zero && item.EquippedMobileId == Serial.Zero)
        {
            _spatialWorldService.AddOrUpdateItem(item, item.MapId);
            _spatialWorldService
                .BroadcastToPlayersInUpdateRadiusAsync(
                    ToolTipHandler.CreateItemPropertyList(item),
                    item.MapId,
                    item.Location
                )
                .GetAwaiter()
                .GetResult();
        }
        else if (_gameNetworkSessionService.TryGet(context.SessionId, out var session))
        {
            _outgoingPacketQueue.Enqueue(session.SessionId, ToolTipHandler.CreateItemPropertyList(item));
        }

        context.Print("Item {0} renamed to '{1}'.", item.Id.Value, newName);
    }

    private void RenameMobile(CommandSystemContext context, UOMobileEntity mobile, string newName)
    {
        mobile.Name = newName;
        _mobileService.CreateOrUpdateAsync(mobile).GetAwaiter().GetResult();
        _spatialWorldService.AddOrUpdateMobile(mobile);

        var maxHits = mobile.MaxHits > 0 ? mobile.MaxHits : Math.Max(mobile.Hits, 1);
        var maxMana = mobile.MaxMana > 0 ? mobile.MaxMana : Math.Max(mobile.Mana, 1);
        var maxStamina = mobile.MaxStamina > 0 ? mobile.MaxStamina : Math.Max(mobile.Stamina, 1);

        _spatialWorldService
            .BroadcastToPlayersInUpdateRadiusAsync(
                MegaClilocBuilder.CreateMobileTooltip(
                    mobile.Id,
                    mobile.Name ?? string.Empty,
                    mobile.Hits,
                    maxHits,
                    mobile.Mana,
                    maxMana,
                    mobile.Stamina,
                    maxStamina,
                    mobile.IsPlayer
                ),
                mobile.MapId,
                mobile.Location
            )
            .GetAwaiter()
            .GetResult();

        if (_gameNetworkSessionService.TryGet(context.SessionId, out var session))
        {
            _outgoingPacketQueue.Enqueue(session.SessionId, new PaperdollPacket(mobile));
        }

        context.Print("Mobile {0} renamed to '{1}'.", mobile.Id.Value, newName);
    }
}
