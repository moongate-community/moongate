using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Items;

public sealed class DoorLockService : IDoorLockService
{
    private readonly IItemService _itemService;
    private readonly IDoorDataService _doorDataService;

    public DoorLockService(IItemService itemService, IDoorDataService doorDataService)
    {
        _itemService = itemService;
        _doorDataService = doorDataService;
    }

    public async Task<DoorLockResult> LockDoorAsync(Serial doorId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var door = await _itemService.GetItemAsync(doorId);

        if (door is null || !IsSupportedDoor(door))
        {
            return new(false, null);
        }

        var lockId = door.TryGetCustomString(ItemCustomParamKeys.Door.LockId, out var existingLockId) &&
                     !string.IsNullOrWhiteSpace(existingLockId)
                         ? existingLockId
                         : Guid.NewGuid().ToString("N");

        ApplyLock(door, lockId!);
        await _itemService.UpsertItemAsync(door);

        var linkedDoor = await TryGetLinkedDoorAsync(door);

        if (linkedDoor is not null)
        {
            ApplyLock(linkedDoor, lockId!);
            await _itemService.UpsertItemAsync(linkedDoor);
        }

        return new(true, lockId);
    }

    public async Task<bool> UnlockDoorAsync(Serial doorId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var door = await _itemService.GetItemAsync(doorId);

        if (door is null || !IsSupportedDoor(door))
        {
            return false;
        }

        ClearLock(door);
        await _itemService.UpsertItemAsync(door);

        var linkedDoor = await TryGetLinkedDoorAsync(door);

        if (linkedDoor is not null)
        {
            ClearLock(linkedDoor);
            await _itemService.UpsertItemAsync(linkedDoor);
        }

        return true;
    }

    private static void ApplyLock(UOItemEntity door, string lockId)
    {
        door.SetCustomBoolean(ItemCustomParamKeys.Door.Locked, true);
        door.SetCustomString(ItemCustomParamKeys.Door.LockId, lockId);
    }

    private static void ClearLock(UOItemEntity door)
    {
        door.RemoveCustomProperty(ItemCustomParamKeys.Door.Locked);
        door.RemoveCustomProperty(ItemCustomParamKeys.Door.LockId);
    }

    private bool IsSupportedDoor(UOItemEntity item)
        => item.IsDoor || _doorDataService.TryGetToggleDefinition(item.ItemId, out _);

    private async Task<UOItemEntity?> TryGetLinkedDoorAsync(UOItemEntity door)
    {
        if (!door.TryGetCustomInteger(ItemCustomParamKeys.Door.LinkSerial, out var linkedSerialValue) ||
            linkedSerialValue <= 0 ||
            linkedSerialValue > uint.MaxValue)
        {
            return null;
        }

        var linkedDoor = await _itemService.GetItemAsync((Serial)(uint)linkedSerialValue);

        return linkedDoor is not null && IsSupportedDoor(linkedDoor) ? linkedDoor : null;
    }
}
