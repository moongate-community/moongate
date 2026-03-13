using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Default door state resolver and toggle implementation.
/// </summary>
public sealed class DoorService : IDoorService
{
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IDoorDataService _doorDataService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IMobileService _mobileService;

    public DoorService(
        IItemService itemService,
        ISpatialWorldService spatialWorldService,
        IDoorDataService doorDataService,
        IGameNetworkSessionService gameNetworkSessionService,
        IMobileService mobileService
    )
    {
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
        _doorDataService = doorDataService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _mobileService = mobileService;
    }

    public async Task<bool> IsDoorAsync(Serial itemId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (itemId == Serial.Zero)
        {
            return false;
        }

        var item = await _itemService.GetItemAsync(itemId);

        return item is not null && IsSupportedDoor(item);
    }

    public async Task<bool> ToggleAsync(Serial itemId, long sessionId = 0, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (itemId == Serial.Zero)
        {
            return false;
        }

        var item = await _itemService.GetItemAsync(itemId);

        if (item is null)
        {
            return false;
        }

        if (!IsSupportedDoor(item))
        {
            return false;
        }

        if (IsClosedAndLocked(item) && !await HasMatchingKeyAsync(sessionId, item))
        {
            return false;
        }

        var toggled = await ToggleCoreAsync(item);

        if (!toggled)
        {
            return false;
        }

        if (!item.TryGetCustomInteger(ItemCustomParamKeys.Door.LinkSerial, out var linkedSerialValue) ||
            linkedSerialValue <= 0 ||
            linkedSerialValue > uint.MaxValue)
        {
            return true;
        }

        if (!IsOpen(item))
        {
            return true;
        }

        var linkedItem = await _itemService.GetItemAsync((Serial)(uint)linkedSerialValue);

        if (linkedItem is null || !IsSupportedDoor(linkedItem) || IsOpen(linkedItem))
        {
            return true;
        }

        await ToggleLinkedCoreAsync(linkedItem);

        return true;
    }

    private bool IsClosedAndLocked(UOItemEntity item)
    {
        if (!_doorDataService.TryGetToggleDefinition(item.ItemId, out var state) || !state.IsClosed)
        {
            return false;
        }

        if (!item.TryGetCustomBoolean(ItemCustomParamKeys.Door.Locked, out var locked) || !locked)
        {
            return false;
        }

        return item.TryGetCustomString(ItemCustomParamKeys.Door.LockId, out var lockId) && !string.IsNullOrWhiteSpace(lockId);
    }

    private async Task<bool> HasMatchingKeyAsync(long sessionId, UOItemEntity door)
    {
        if (sessionId == 0 ||
            !_gameNetworkSessionService.TryGet(sessionId, out var session) ||
            !door.TryGetCustomString(ItemCustomParamKeys.Door.LockId, out var lockId) ||
            string.IsNullOrWhiteSpace(lockId))
        {
            return false;
        }

        var mobile = session.Character;

        if (mobile is null && session.CharacterId != Serial.Zero)
        {
            mobile = await _mobileService.GetAsync(session.CharacterId);
        }

        if (mobile is null)
        {
            return false;
        }

        var visited = new HashSet<Serial>();

        foreach (var equippedItemId in mobile.EquippedItemIds.Values)
        {
            if (equippedItemId == Serial.Zero)
            {
                continue;
            }

            if (await ContainsMatchingKeyRecursiveAsync(equippedItemId, lockId!, visited))
            {
                return true;
            }
        }

        if (mobile.BackpackId != Serial.Zero)
        {
            return await ContainsMatchingKeyRecursiveAsync(mobile.BackpackId, lockId!, visited);
        }

        return false;
    }

    private async Task<bool> ContainsMatchingKeyRecursiveAsync(Serial itemId, string lockId, HashSet<Serial> visited)
    {
        if (!visited.Add(itemId))
        {
            return false;
        }

        var item = await _itemService.GetItemAsync(itemId);

        if (item is null)
        {
            return false;
        }

        if (item.TryGetCustomString(ItemCustomParamKeys.Key.LockId, out var itemLockId) &&
            string.Equals(itemLockId, lockId, StringComparison.Ordinal))
        {
            return true;
        }

        var containedItems = await _itemService.GetItemsInContainerAsync(item.Id);

        foreach (var containedItem in containedItems)
        {
            if (await ContainsMatchingKeyRecursiveAsync(containedItem.Id, lockId, visited))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSupportedDoor(UOItemEntity item)
        => item.IsDoor || _doorDataService.TryGetToggleDefinition(item.ItemId, out _);

    private bool IsOpen(UOItemEntity item)
    {
        if (!_doorDataService.TryGetToggleDefinition(item.ItemId, out var state))
        {
            return false;
        }

        return !state.IsClosed;
    }

    private async Task<bool> ToggleCoreAsync(UOItemEntity item)
    {
        if (!_doorDataService.TryGetToggleDefinition(item.ItemId, out var state))
        {
            return false;
        }

        var targetLocation = state.IsClosed
            ? new Point3D(
                item.Location.X + state.Offset.X,
                item.Location.Y + state.Offset.Y,
                item.Location.Z + state.Offset.Z
            )
            : new Point3D(
                item.Location.X - state.Offset.X,
                item.Location.Y - state.Offset.Y,
                item.Location.Z - state.Offset.Z
            );

        var moved = await _itemService.MoveItemToWorldAsync(item.Id, targetLocation, item.MapId);

        if (!moved)
        {
            return false;
        }

        // MoveItemToWorldAsync operates on an internal clone; sync location on our reference
        // so the subsequent UpsertItemAsync does not revert the position change.
        item.Location = targetLocation;
        item.ItemId = state.NextItemId;
        await _itemService.UpsertItemAsync(item);
        _spatialWorldService.AddOrUpdateItem(item, item.MapId);

        return true;
    }

    private async Task<bool> ToggleLinkedCoreAsync(UOItemEntity linkedDoor)
    {
        if (!_doorDataService.TryGetToggleDefinition(linkedDoor.ItemId, out var linkedState))
        {
            return false;
        }

        var targetLocation = new Point3D(
            linkedDoor.Location.X + linkedState.Offset.X,
            linkedDoor.Location.Y + linkedState.Offset.Y,
            linkedDoor.Location.Z + linkedState.Offset.Z
        );

        var moved = await _itemService.MoveItemToWorldAsync(linkedDoor.Id, targetLocation, linkedDoor.MapId);

        if (!moved)
        {
            return false;
        }

        // MoveItemToWorldAsync operates on an internal clone; sync location on our reference
        // so the subsequent UpsertItemAsync does not revert the position change.
        linkedDoor.Location = targetLocation;
        linkedDoor.ItemId = linkedState.NextItemId;
        await _itemService.UpsertItemAsync(linkedDoor);
        _spatialWorldService.AddOrUpdateItem(linkedDoor, linkedDoor.MapId);

        return true;
    }
}
