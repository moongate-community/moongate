using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class ItemService : IItemService
{
    public event IItemService.ItemEventHandler? ItemCreated;
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
            _items[item.Id] = item;
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
        var lastSerial = new Serial(Serial.MaxItemSerial);

        if (_items.Count > 0)
        {
            lastSerial = _items.Keys.Last() + 1;
        }

        var item = new UOItemEntity()
        {
            Id = lastSerial,
        };

        _items[item.Id] = item;

        ItemCreated?.Invoke(item);

        _saveLock.Release();

        return item;
    }

    public UOItemEntity? GetItem(Serial id)
    {
        _saveLock.Wait();
        if (_items.TryGetValue(id, out var item))
        {
            _saveLock.Release();
            return item;
        }

        _saveLock.Release();
        return null;
    }

    public void Dispose()
    {
    }
}
