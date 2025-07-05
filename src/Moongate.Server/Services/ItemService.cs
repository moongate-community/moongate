using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Server.Services;

public class ItemService : IItemService
{
    public event IItemService.ItemEventHandler? ItemCreated;
    public event IItemService.ItemEventHandler? ItemAdded;
    public event IItemService.ItemMovedEventHandler? ItemMoved;
    private readonly ILogger _logger = Log.ForContext<MobileService>();
    private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    private const string itemsFilePath = "items.mga";

    private readonly Dictionary<Serial, UOItemEntity> _items = new();

    private readonly IEntityFileService _entityFileService;

    public ItemService(IEntityFileService entityFileService)
    {
        _entityFileService = entityFileService;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Loading items from file...");

        return LoadAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Saving {Count} items to file...", _items.Count);
        return SaveAsync(cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        _items.Clear();

        var items = await _entityFileService.LoadEntitiesAsync<UOItemEntity>(itemsFilePath);

        foreach (var item in items)
        {
            AddItem(item);
        }

        _saveLock.Release();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        _logger.Information("Saving {Count} items to file...", _items.Count);
        await _entityFileService.SaveEntitiesAsync(itemsFilePath, _items.Values);

        _saveLock.Release();
    }


    public UOItemEntity CreateItem()
    {
        _saveLock.Wait();
        var lastSerial = new Serial(Serial.ItemOffset);

        if (_items.Count > 0)
        {
            lastSerial = _items.Keys.Last() + 1;
        }

        var item = new UOItemEntity()
        {
            Id = lastSerial,
        };


        ItemCreated?.Invoke(item);

        _saveLock.Release();

        return item;
    }

    public UOItemEntity CreateItemAndAdd()
    {
        var item = CreateItem();
        AddItem(item);
        return item;
    }


    public void AddItem(UOItemEntity item)
    {
        if (!_items.TryAdd(item.Id, item))
        {
            _logger.Warning("Item with ID {Id} already exists, not adding again.", item.Id);
            return;
        }

        item.ItemMoved += ItemOnItemMoved;
        ItemAdded?.Invoke(item);
    }

    private void ItemOnItemMoved(UOItemEntity item, Point3D oldLocation, Point3D newLocation)
    {
        ItemMoved?.Invoke(item, oldLocation, newLocation);
    }

    public void UseItem(UOItemEntity item, UOMobileEntity? user)
    {
        if (string.IsNullOrEmpty(item.ScriptId))
        {
            _logger.Warning("Item {Id} does not have a script ID, cannot use.", item.Id);
            return;
        }

    }

    public UOItemEntity? GetItem(Serial id)
    {
        return _items.GetValueOrDefault(id);
    }

    public void Dispose()
    {
    }
}
