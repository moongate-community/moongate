using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class ItemService : IItemService
{
    private readonly ILogger _logger = Log.ForContext<MobileService>();

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
        _items.Clear();

        var items = await _entityFileService.LoadEntitiesAsync<UOItemEntity>(itemsFilePath);

        foreach (var item in items)
        {
            _items[item.Id] = item;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Saving {Count} items to file...", _items.Count);
        await _entityFileService.SaveEntitiesAsync(itemsFilePath, _items.Values);
    }

    public void Dispose()
    {
    }
}
