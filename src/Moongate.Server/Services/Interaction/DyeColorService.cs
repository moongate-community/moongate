using System.Collections.Concurrent;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Utils;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.Interaction;

public sealed class DyeColorService : IDyeColorService
{
    private readonly ILogger _logger = Log.ForContext<DyeColorService>();
    private readonly IPlayerTargetService _playerTargetService;
    private readonly IItemService _itemService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly ICharacterService _characterService;
    private readonly ConcurrentDictionary<long, PendingDyeRequest> _pendingRequests = new();

    public DyeColorService(
        IPlayerTargetService playerTargetService,
        IItemService itemService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue,
        ISpatialWorldService spatialWorldService,
        ICharacterService characterService
    )
    {
        _playerTargetService = playerTargetService;
        _itemService = itemService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _spatialWorldService = spatialWorldService;
        _characterService = characterService;
    }

    private sealed record PendingDyeRequest(Serial TargetSerial, Serial DyeTubSerial, ushort Model, DateTime CreatedAt);

    public async Task<bool> BeginAsync(
        long sessionId,
        Serial dyeTubSerial,
        Func<UOItemEntity, bool>? targetSelectedCallback = null
    )
    {
        if (!_gameNetworkSessionService.TryGet(sessionId, out var session) || session.CharacterId == Serial.Zero)
        {
            return false;
        }

        var dyeTub = await _itemService.GetItemAsync(dyeTubSerial);

        if (dyeTub is null)
        {
            return false;
        }

        await _playerTargetService.SendTargetCursorAsync(
            sessionId,
            callback =>
            {
                HandleTargetSelection(
                    session,
                    dyeTubSerial,
                    callback.Packet,
                    targetSelectedCallback
                );
            },
            TargetCursorSelectionType.SelectObject,
            TargetCursorType.Helpful
        );

        return true;
    }

    public async Task<bool> HandleResponseAsync(GameSession session, DyeWindowPacket packet)
    {
        if (!_pendingRequests.TryRemove(session.SessionId, out var pending))
        {
            return false;
        }

        if (pending.TargetSerial != (Serial)packet.TargetSerial)
        {
            return false;
        }

        var item = await _itemService.GetItemAsync(pending.TargetSerial);

        if (item is null || !IsDyeable(item))
        {
            return false;
        }

        item.Hue = packet.Hue & 0x3FFF;
        await _itemService.UpsertItemAsync(item);
        _spatialWorldService.AddOrUpdateItem(item, item.MapId);

        await RefreshVisualsAsync(session, item);

        return true;
    }

    public async Task<bool> SendDyeableAsync(
        long sessionId,
        Serial itemSerial,
        ushort model = DisplayDyeWindowPacket.DefaultModel
    )
    {
        if (!_gameNetworkSessionService.TryGet(sessionId, out var session))
        {
            return false;
        }

        var item = await _itemService.GetItemAsync(itemSerial);

        if (item is null || !await CanAccessItemAsync(session, item) || !IsDyeable(item))
        {
            return false;
        }

        OpenDyeWindow(session, item, model);

        return true;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    private async Task<bool> CanAccessItemAsync(GameSession session, UOItemEntity item)
    {
        if (session.CharacterId == Serial.Zero)
        {
            return false;
        }

        if (item.EquippedMobileId == session.CharacterId)
        {
            return true;
        }

        var mobile = await _characterService.GetCharacterAsync(session.CharacterId);

        if (mobile is null)
        {
            return false;
        }

        var backpackId = ResolveBackpackId(mobile);

        if (backpackId == Serial.Zero)
        {
            return false;
        }

        var currentContainerId = item.ParentContainerId;

        while (currentContainerId != Serial.Zero)
        {
            if (currentContainerId == backpackId)
            {
                return true;
            }

            var container = await _itemService.GetItemAsync(currentContainerId);

            if (container is null)
            {
                return false;
            }

            if (container.EquippedMobileId == session.CharacterId)
            {
                return true;
            }

            currentContainerId = container.ParentContainerId;
        }

        return false;
    }

    private void HandleTargetSelection(
        GameSession session,
        Serial dyeTubSerial,
        TargetCursorCommandsPacket packet,
        Func<UOItemEntity, bool>? targetSelectedCallback
    )
    {
        if (packet.CursorType == TargetCursorType.CancelCurrentTargeting || packet.ClickedOnId == Serial.Zero)
        {
            return;
        }

        var item = _itemService.GetItemAsync(packet.ClickedOnId).GetAwaiter().GetResult();

        if (item is null)
        {
            return;
        }

        if (!CanAccessItemAsync(session, item).GetAwaiter().GetResult())
        {
            return;
        }

        if (targetSelectedCallback is not null && !targetSelectedCallback(item))
        {
            return;
        }

        if (!IsDyeable(item))
        {
            return;
        }

        OpenDyeWindow(session, item, DisplayDyeWindowPacket.DefaultModel, dyeTubSerial);
    }

    private static bool IsDyeable(UOItemEntity item)
        => item.TryGetCustomBoolean(ItemCustomParamKeys.Item.Dyeable, out var dyeable) && dyeable;

    private void OpenDyeWindow(GameSession session, UOItemEntity item, ushort model, Serial dyeTubSerial = default)
    {
        _pendingRequests[session.SessionId] = new(item.Id, dyeTubSerial, model, DateTime.UtcNow);
        _outgoingPacketQueue.Enqueue(session.SessionId, new DisplayDyeWindowPacket(item.Id, model));
    }

    private async Task RefreshVisualsAsync(GameSession session, UOItemEntity item)
    {
        if (item.EquippedMobileId != Serial.Zero && item.EquippedLayer is { } equippedLayer)
        {
            var character = await _characterService.GetCharacterAsync(item.EquippedMobileId);

            if (character is not null)
            {
                var reference = new ItemReference(item.Id, item.ItemId, item.Hue);
                await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
                    new WornItemPacket(character, reference, equippedLayer),
                    character.MapId,
                    character.Location
                );
            }

            return;
        }

        if (item.ParentContainerId != Serial.Zero)
        {
            var container = await _itemService.GetItemAsync(item.ParentContainerId);

            if (container is not null)
            {
                _outgoingPacketQueue.Enqueue(session.SessionId, new DrawContainerAndAddItemCombinedPacket(container));
            }

            return;
        }

        await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
            ItemPacketHelper.CreateObjectInformationPacket(item, session),
            item.MapId,
            item.Location
        );
    }

    private static Serial ResolveBackpackId(UOMobileEntity mobile)
    {
        if (mobile.BackpackId != Serial.Zero)
        {
            return mobile.BackpackId;
        }

        return mobile.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var backpackId)
                   ? backpackId
                   : Serial.Zero;
    }
}
