using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

public sealed class BandageService : IBandageService
{
    private const int BandageItemId = 0x0E21;
    private const int HealAmount = 12;
    private static readonly TimeSpan BandageDelay = TimeSpan.FromSeconds(3);

    private readonly ITimerService _timerService;
    private readonly IMobileService _mobileService;
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<Serial, string> _inFlightTimers = [];

    public BandageService(
        ITimerService timerService,
        IMobileService mobileService,
        IItemService itemService,
        ISpatialWorldService spatialWorldService
    )
    {
        _timerService = timerService;
        _mobileService = mobileService;
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
    }

    public async Task<bool> BeginSelfBandageAsync(Serial mobileId, CancellationToken cancellationToken = default)
    {
        if (mobileId == Serial.Zero || IsBandaging(mobileId))
        {
            return false;
        }

        var mobile = await ResolveMobileAsync(mobileId, cancellationToken);

        if (mobile is null || !mobile.IsAlive || mobile.Hits >= mobile.MaxHits)
        {
            return false;
        }

        if (!TryGetBackpack(mobile, out var backpack))
        {
            return false;
        }

        if (!TryConsumeBandage(backpack, out var changedStack, out var deletedStack))
        {
            return false;
        }

        if (changedStack is not null)
        {
            await _itemService.UpsertItemAsync(changedStack);
        }

        if (deletedStack is not null)
        {
            _ = await _itemService.DeleteItemAsync(deletedStack.Id);
        }

        var timerName = GetTimerName(mobileId);
        var timerId = _timerService.RegisterTimer(
            timerName,
            BandageDelay,
            () => CompleteSelfBandage(mobileId),
            BandageDelay
        );

        lock (_syncRoot)
        {
            _inFlightTimers[mobileId] = timerId;
        }

        return true;
    }

    public async Task<bool> HasBandageAsync(Serial mobileId, CancellationToken cancellationToken = default)
    {
        if (mobileId == Serial.Zero)
        {
            return false;
        }

        var mobile = await ResolveMobileAsync(mobileId, cancellationToken);

        if (mobile is null || !TryGetBackpack(mobile, out var backpack))
        {
            return false;
        }

        return ContainsBandage(backpack);
    }

    public bool IsBandaging(Serial mobileId)
    {
        lock (_syncRoot)
        {
            return _inFlightTimers.ContainsKey(mobileId);
        }
    }

    private void ClearInFlight(Serial mobileId)
    {
        lock (_syncRoot)
        {
            _inFlightTimers.Remove(mobileId);
        }
    }

    private void CompleteSelfBandage(Serial mobileId)
    {
        ClearInFlight(mobileId);

        var mobile = ResolveMobile(mobileId);

        if (mobile is null || !mobile.IsAlive || mobile.Hits >= mobile.MaxHits)
        {
            return;
        }

        mobile.Hits = Math.Min(mobile.MaxHits, mobile.Hits + HealAmount);
        _mobileService.CreateOrUpdateAsync(mobile).GetAwaiter().GetResult();
    }

    private static bool ContainsBandage(UOItemEntity container)
    {
        for (var index = container.Items.Count - 1; index >= 0; index--)
        {
            var child = container.Items[index];

            if (child.ItemId == BandageItemId)
            {
                return true;
            }

            if (ContainsBandage(child))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetTimerName(Serial mobileId)
        => $"bandage:{(uint)mobileId}";

    private UOMobileEntity? ResolveMobile(Serial mobileId)
    {
        foreach (var sector in _spatialWorldService.GetActiveSectors())
        {
            var mobile = sector.GetEntity<UOMobileEntity>(mobileId);

            if (mobile is not null)
            {
                return mobile;
            }
        }

        return null;
    }

    private async Task<UOMobileEntity?> ResolveMobileAsync(Serial mobileId, CancellationToken cancellationToken)
    {
        var mobile = ResolveMobile(mobileId);

        if (mobile is not null)
        {
            return mobile;
        }

        return await _mobileService.GetAsync(mobileId, cancellationToken);
    }

    private static bool TryConsumeBandage(
        UOItemEntity container,
        out UOItemEntity? changedStack,
        out UOItemEntity? deletedStack
    )
    {
        changedStack = null;
        deletedStack = null;

        for (var index = container.Items.Count - 1; index >= 0; index--)
        {
            var child = container.Items[index];

            if (child.ItemId == BandageItemId)
            {
                child.Amount--;

                if (child.Amount <= 0)
                {
                    container.RemoveItem(child.Id);
                    deletedStack = child;
                }
                else
                {
                    changedStack = child;
                }

                return true;
            }

            if (TryConsumeBandage(child, out changedStack, out deletedStack))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBackpack(UOMobileEntity mobile, out UOItemEntity backpack)
    {
        backpack = null!;

        foreach (var equippedItem in mobile.GetEquippedItemsRuntime())
        {
            if (equippedItem.EquippedLayer == ItemLayerType.Backpack ||
                equippedItem.Id == mobile.BackpackId)
            {
                backpack = equippedItem;

                return true;
            }
        }

        return false;
    }
}
