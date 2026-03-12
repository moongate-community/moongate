using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Default door state resolver and toggle implementation.
/// </summary>
public sealed class DoorService : IDoorService
{
    private const string DoorLinkSerialCustomFieldKey = "door_link_serial";

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

        if (!item.TryGetCustomInteger(DoorLinkSerialCustomFieldKey, out var linkedSerialValue) ||
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

        var primaryDelta = new Point3D(
            item.Location.X - originalLocation.X,
            item.Location.Y - originalLocation.Y,
            item.Location.Z - originalLocation.Z
        );
        await ToggleLinkedCoreAsync(originalLocation, linkedItem, primaryDelta);

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

        item.ItemId = state.NextItemId;
        await _itemService.UpsertItemAsync(item);
        _spatialWorldService.AddOrUpdateItem(item, item.MapId);

        return true;
    }

    private async Task<bool> ToggleLinkedCoreAsync(
        Point3D primaryClosedLocation,
        UOItemEntity linkedDoor,
        Point3D primaryDelta
    )
    {
        if (!_doorDataService.TryGetToggleDefinition(linkedDoor.ItemId, out var linkedState))
        {
            return false;
        }

        var offset = ResolveLinkedOpenOffset(primaryClosedLocation, linkedDoor.Location, linkedState.Offset, primaryDelta);
        var targetLocation = new Point3D(
            linkedDoor.Location.X + offset.X,
            linkedDoor.Location.Y + offset.Y,
            linkedDoor.Location.Z + offset.Z
        );

        var moved = await _itemService.MoveItemToWorldAsync(linkedDoor.Id, targetLocation, linkedDoor.MapId);

        if (!moved)
        {
            return false;
        }

        linkedDoor.ItemId = linkedState.NextItemId;
        await _itemService.UpsertItemAsync(linkedDoor);
        _spatialWorldService.AddOrUpdateItem(linkedDoor, linkedDoor.MapId);

        return true;
    }

    private static Point3D ResolveLinkedOpenOffset(
        Point3D primaryLocation,
        Point3D linkedLocation,
        Point3D linkedDefaultOffset,
        Point3D primaryDelta
    )
    {
        var doorDeltaX = linkedLocation.X - primaryLocation.X;
        var doorDeltaY = linkedLocation.Y - primaryLocation.Y;
        var offsetX = linkedDefaultOffset.X;
        var offsetY = linkedDefaultOffset.Y;

        // For linked double doors, mirror the perpendicular component so the pair opens opposite ways.
        if (Math.Abs(doorDeltaX) == 1 && doorDeltaY == 0)
        {
            var primaryPerpendicular = primaryDelta.Y != 0 ? primaryDelta.Y : linkedDefaultOffset.Y;
            offsetY = -primaryPerpendicular;
        }
        else if (Math.Abs(doorDeltaY) == 1 && doorDeltaX == 0)
        {
            var primaryPerpendicular = primaryDelta.X != 0 ? primaryDelta.X : linkedDefaultOffset.X;
            offsetX = -primaryPerpendicular;
        }

        return new(offsetX, offsetY, linkedDefaultOffset.Z);
    }
}
