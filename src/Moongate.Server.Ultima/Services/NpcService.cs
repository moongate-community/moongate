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
    private readonly INpcScriptService? _scripts;

    public NpcService(
        IMobileFactoryService factory,
        IMobileService mobiles,
        IItemService items,
        IWorldViewService view,
        IDataAccess<MobileEntity> mobileData,
        IDataAccess<ItemEntity> itemData,
        IGameLoopService loop,
        INpcScriptService? scripts = null
    )
    {
        _factory = factory;
        _mobiles = mobiles;
        _items = items;
        _view = view;
        _mobileData = mobileData;
        _itemData = itemData;
        _loop = loop;
        _scripts = scripts;
    }

    public async Task StartAsync()
    {
        var npcs = await _mobileData.QueryAsync(mobile => mobile.AccountId == null);
        var ids = npcs.Select(mobile => (Serial?)mobile.Id).ToList();
        var worn = await _itemData.QueryAsync(item => ids.Contains(item.MobileId));
        var items = await ItemContentsLoader.LoadAsync(_itemData, worn);

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
                _scripts?.Queue(npc, "on_spawn");
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
