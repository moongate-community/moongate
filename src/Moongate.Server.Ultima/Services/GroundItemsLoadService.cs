using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Loads the items lying on the ground, and everything inside them, into the live world at startup; the world save
///     keeps them from then on.
/// </summary>
public sealed class GroundItemsLoadService : IMoongateStartupService
{
    private readonly ILogger _logger = Log.ForContext<GroundItemsLoadService>();
    private readonly IDataAccess<ItemEntity> _items;
    private readonly IItemService _live;
    private readonly IGameLoopService _loop;

    public GroundItemsLoadService(IDataAccess<ItemEntity> items, IItemService live, IGameLoopService loop)
    {
        _items = items;
        _live = live;
        _loop = loop;
    }

    public async Task StartAsync()
    {
        var roots = await _items.QueryAsync(item => item.Map != null);
        var loaded = new List<ItemEntity>(roots);
        var visited = roots.Select(item => item.Id).ToHashSet();
        var containers = visited.Select(serial => (Serial?)serial).ToList();

        // One level of containers at a time, as a character's items at login; the visited set stops a cycle.
        while (containers.Count > 0)
        {
            var level = await _items.QueryAsync(item => containers.Contains(item.ContainerId));
            var fresh = level.Where(item => visited.Add(item.Id)).ToList();
            loaded.AddRange(fresh);
            containers = fresh.Select(item => (Serial?)item.Id).ToList();
        }

        // The live items and the sector grid change only on the game loop.
        var work = new LoopActionWorkItem(() => _live.Add(loaded));
        await _loop.PostAsync(work);
        await work.Completion;
        _logger.Information("Loaded {Roots} items on the ground, {Total} with their contents", roots.Count, loaded.Count);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
