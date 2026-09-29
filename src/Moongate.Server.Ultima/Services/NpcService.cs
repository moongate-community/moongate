using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Loads the NPCs, the mobiles without an account, with what they wear and carry at startup, spawns new ones and
///     removes them; the live world and the sector grid change on the game loop.
/// </summary>
public sealed class NpcService : INpcService
{
    private readonly ILogger _logger = Log.ForContext<NpcService>();
    private readonly IMobileFactoryService _factory;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IWorldViewService _view;
    private readonly IDataAccess<MobileEntity> _mobileData;
    private readonly IDataAccess<ItemEntity> _itemData;
    private readonly IGameLoopService _loop;

    public NpcService(
        IMobileFactoryService factory,
        IMobileService mobiles,
        IItemService items,
        IWorldViewService view,
        IDataAccess<MobileEntity> mobileData,
        IDataAccess<ItemEntity> itemData,
        IGameLoopService loop
    )
    {
        _factory = factory;
        _mobiles = mobiles;
        _items = items;
        _view = view;
        _mobileData = mobileData;
        _itemData = itemData;
        _loop = loop;
    }

    public async Task StartAsync()
    {
        var npcs = await _mobileData.QueryAsync(mobile => mobile.AccountId == null);
        var ids = npcs.Select(mobile => (Serial?)mobile.Id).ToList();
        var items = new List<ItemEntity>(await _itemData.QueryAsync(item => ids.Contains(item.MobileId)));
        var visited = items.Select(item => item.Id).ToHashSet();
        var containers = visited.Select(serial => (Serial?)serial).ToList();

        // One level of containers at a time, as a character's items at login; the visited set stops a cycle.
        while (containers.Count > 0)
        {
            var level = await _itemData.QueryAsync(item => containers.Contains(item.ContainerId));
            var fresh = level.Where(item => visited.Add(item.Id)).ToList();
            items.AddRange(fresh);
            containers = fresh.Select(item => (Serial?)item.Id).ToList();
        }

        await OnLoopAsync(
            () =>
            {
                foreach (var npc in npcs)
                {
                    _mobiles.EnterWorld(npc);
                }

                _items.Add(items);
            },
            CancellationToken.None
        );
        _logger.Information("Loaded {Npcs} NPCs with {Items} items", npcs.Count, items.Count);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<MobileEntity> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        CancellationToken cancellationToken = default
    )
    {
        var spawned = await _factory.SpawnAsync(templateId, map, location, cancellationToken);
        var npc = spawned.Mobile;

        // Saved already: from here it is live whatever the caller does.
        await OnLoopAsync(
            () =>
            {
                _mobiles.EnterWorld(npc);
                _items.Add(spawned.Equipment.Append(spawned.Backpack).Concat(spawned.BackpackItems));
                _view.MobileAppeared(npc);
            },
            CancellationToken.None
        );

        return npc;
    }

    public async Task<bool> RemoveAsync(Serial serial, CancellationToken cancellationToken = default)
    {
        var removed = false;
        await OnLoopAsync(
            () =>
            {
                if (!_mobiles.TryGet(serial, out var npc) || !npc.IsNpc)
                {
                    return;
                }

                // Still in the grid: the players around it can be told.
                _view.Left(npc);
                _items.Remove(_items.GetOwnedBy(npc.Id).Select(item => item.Id));
                removed = _mobiles.Delete(npc.Id);
            },
            cancellationToken
        );

        return removed;
    }

    private async Task OnLoopAsync(Action action, CancellationToken cancellationToken)
    {
        var work = new LoopActionWorkItem(action);
        await _loop.PostAsync(work, cancellationToken);
        await work.Completion;
    }
}
