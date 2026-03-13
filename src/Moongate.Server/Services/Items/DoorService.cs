using Moongate.Server.Interfaces.Items;
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

    public DoorService(
        IItemService itemService,
        ISpatialWorldService spatialWorldService,
        IDoorDataService doorDataService
    )
    {
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
        _doorDataService = doorDataService;
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

    public async Task<bool> ToggleAsync(Serial itemId, CancellationToken cancellationToken = default)
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

        var originalLocation = item.Location;
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
