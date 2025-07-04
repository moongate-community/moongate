using System.Collections.Concurrent;
using System.ComponentModel;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.MegaCliloc;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class MegaClilocService : IMegaClilocService
{
    private readonly IItemService _itemService;
    private readonly IMobileService _mobileService;


    private readonly ILogger _logger = Log.ForContext<MegaClilocService>();

    private readonly ConcurrentDictionary<Serial, MegaClilocEntry> _entries = new();

    public MegaClilocService(IItemService itemService, IMobileService mobileService)
    {
        _itemService = itemService;
        _mobileService = mobileService;
    }

    public void Dispose()
    {
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _mobileService.MobileAdded += MobileServiceOnMobileAdded;
        _itemService.ItemAdded += ItemServiceOnItemCreated;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _mobileService.MobileAdded -= MobileServiceOnMobileAdded;
        _itemService.ItemAdded -= ItemServiceOnItemCreated;
    }

    private void ItemServiceOnItemCreated(UOItemEntity item)
    {
        item.PropertyChanged += ItemOnPropertyChanged;

        RebuildPropertiesItemAsync(item);
    }

    private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RebuildPropertiesItemAsync(sender as UOItemEntity);
    }

    private void MobileServiceOnMobileAdded(UOMobileEntity mobile)
    {
        mobile.PropertyChanged += MobileOnPropertyChanged;

        RebuildPropertiesMobileAsync(mobile);
    }

    private void MobileOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    public async Task<MegaClilocEntry> GetMegaClilocEntryAsync(ISerialEntity entity)
    {
        if (_entries.TryGetValue(entity.Id, out var entry))
        {
            return entry;
        }

        await RebuildProperties(entity);

        return _entries[entry.Serial];
    }

    private async Task RebuildProperties(ISerialEntity entity)
    {
        _logger.Debug(
            "Starting to rebuild properties for MegaCliloc: {EntityId} {ObjectType}",
            entity.Id,
            entity.Id.IsItem ? "Item" : "Mobile"
        );

        if (entity.Id.IsItem)
        {
            await RebuildPropertiesItemAsync(entity);
        }
        else if (entity.Id.IsMobile)
        {
            await RebuildPropertiesMobileAsync(entity);
        }
        else
        {
            _logger.Warning("Unknown entity type for MegaCliloc: {EntityId}", entity.Id);
        }
    }

    private async Task RebuildPropertiesMobileAsync(ISerialEntity entity)
    {
        var mobile = _mobileService.GetMobile(entity.Id);

        if (mobile == null)
        {
            throw new InvalidOperationException($"Mobile with ID {entity.Id} not found.");
        }

        _logger.Debug("Rebuilding properties for mobile: {MobileId} {MobileName}", mobile.Id, mobile.Name);

        var entry = new MegaClilocEntry()
        {
            Serial = entity.Id,
        };

        entry.AddProperty(0x1005BD, mobile.Name + " " + mobile.Title);

        _entries.TryAdd(entity.Id, entry);
    }

    private async Task RebuildPropertiesItemAsync(ISerialEntity entity)
    {
        var item = _itemService.GetItem(entity.Id);

        if (item == null)
        {
            throw new InvalidOperationException($"Item with ID {entity.Id} not found.");
        }

        _logger.Debug("Rebuilding properties for item: {ItemId} {ItemName}", item.Id, item.Name);

        var entry = new MegaClilocEntry()
        {
            Serial = entity.Id,
        };

        entry.AddProperty(CommonClilocIds.ObjectName, item.Name);

        _entries.TryAdd(entity.Id, entry);
    }
}
