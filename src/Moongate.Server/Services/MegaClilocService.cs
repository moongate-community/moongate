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

        RebuildPropertiesItemAsync(item.Id);
    }

    private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var entity = sender as UOItemEntity;
        RebuildPropertiesItemAsync(entity.Id);
    }

    private void MobileServiceOnMobileAdded(UOMobileEntity mobile)
    {
        mobile.PropertyChanged += MobileOnPropertyChanged;

        RebuildPropertiesMobileAsync(mobile.Id);
    }

    private void MobileOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    public async Task<MegaClilocEntry> GetMegaClilocEntryAsync(ISerialEntity entity)
    {
        return await GetMegaClilocEntryAsync(entity.Id);
    }

    public async Task<MegaClilocEntry> GetMegaClilocEntryAsync(Serial serial)
    {
        if (_entries.TryGetValue(serial, out var entry))
        {
            return entry;
        }

        await RebuildProperties(serial);

        return _entries[serial];
    }

    private async Task RebuildProperties(Serial entity)
    {
        _logger.Debug(
            "Starting to rebuild properties for MegaCliloc: {EntityId} {ObjectType}",
            entity,
            entity.IsItem ? "Item" : "Mobile"
        );

        if (entity.IsItem)
        {
            await RebuildPropertiesItemAsync(entity);
        }
        else if (entity.IsMobile)
        {
            await RebuildPropertiesMobileAsync(entity);
        }
        else
        {
            _logger.Warning("Unknown entity type for MegaCliloc: {EntityId}", entity);
        }
    }

    private async Task RebuildPropertiesMobileAsync(Serial serial)
    {
        var mobile = _mobileService.GetMobile(serial);

        if (mobile == null)
        {
            throw new InvalidOperationException($"Mobile with ID {serial} not found.");
        }

        _logger.Debug("Rebuilding properties for mobile: {MobileId} {MobileName}", mobile.Id, mobile.Name);

        var entry = new MegaClilocEntry()
        {
            Serial = serial
        };

        entry.AddProperty(0x1005BD, mobile.Name + " " + mobile.Title);

        _entries.TryAdd(serial, entry);
    }

    private async Task RebuildPropertiesItemAsync(Serial serial)
    {
        var item = _itemService.GetItem(serial);

        if (item == null)
        {
            throw new InvalidOperationException($"Item with ID {serial} not found.");
        }

        _logger.Debug("Rebuilding properties for item: {ItemId} {ItemName}", item.Id, item.Name);

        var entry = new MegaClilocEntry()
        {
            Serial = serial
        };

        entry.AddProperty(CommonClilocIds.ItemName, item.Name);

        _entries.TryAdd(serial, entry);
    }
}
