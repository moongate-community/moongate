using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Actions;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class ItemService : IItemService
{
    public event IItemService.ItemEventHandler? ItemCreated;
    public event IItemService.ItemEventHandler? ItemAdded;
    public event IItemService.ItemMovedEventHandler? ItemMoved;
    private readonly ILogger _logger = Log.ForContext<MobileService>();

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private readonly Dictionary<string, IItemAction> _itemActions = new();

    private const string itemsFilePath = "items.mga";

    private readonly Dictionary<Serial, UOItemEntity> _items = new();

    private readonly IEntityFileService _entityFileService;
    private readonly IEventLoopService _eventLoopService;

    private readonly IGameSessionService _gameSessionService;

    private Serial _lastSerial = new Serial(Serial.ItemOffset);


    public ItemService(
        IEntityFileService entityFileService, IEventLoopService eventLoopService, IGameSessionService gameSessionService
    )
    {
        _entityFileService = entityFileService;
        _eventLoopService = eventLoopService;
        _gameSessionService = gameSessionService;
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

        _lastSerial = items.Count > 0
            ? items.Max(i => i.Id)
            : new Serial(Serial.ItemOffset);

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


        _lastSerial += 1;

        var item = new UOItemEntity()
        {
            Id = _lastSerial,
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

    private void ItemOnItemMoved(UOItemEntity item, Point3D oldLocation, Point3D newLocation, bool isOnGround)
    {
        ItemMoved?.Invoke(item, oldLocation, newLocation, isOnGround);
    }

    public void UseItem(UOItemEntity item, UOMobileEntity? user)
    {
        if (string.IsNullOrEmpty(item.ScriptId))
        {
            _logger.Warning("Item {Id} does not have a script ID, cannot use.", item.Id);

            return;
        }

        if (!_itemActions.TryGetValue(item.ScriptId, out var itemAction))
        {
            _logger.Warning("No item action found for {ItemId}, cannot use item.", item.ScriptId);

            return;
        }

        var gameSession = _gameSessionService.GetGameSessionByMobile(user);

        _eventLoopService.EnqueueAction(
            $"use_item_{item.Id.ToString()}",
            () => { itemAction.OnUseItem(ItemUseContext.Create(gameSession, item, user)); }
        );
    }

    public void AddItemActionScript(string itemId, IItemAction itemAction)
    {
        if (_itemActions.ContainsKey(itemId))
        {
            _logger.Warning("Item action for {ItemId} already exists, replacing.", itemId);
        }

        _itemActions[itemId] = itemAction;
        _logger.Information("Added item action for {ItemId}.", itemId);
    }

    public void RemoveItemFromWorld(UOItemEntity item)
    {
        if (!_items.Remove(item.Id))
        {
            _logger.Warning("Item with ID {Id} not found, cannot remove.", item.Id);
            return;
        }

        item.ItemMoved -= ItemOnItemMoved;

        _logger.Information("Removed item with ID {Id} from world.", item.Id);
    }

    public UOItemEntity? GetItem(Serial id)
    {
        return _items.GetValueOrDefault(id);
    }

    public void Dispose()
    {
    }
}
