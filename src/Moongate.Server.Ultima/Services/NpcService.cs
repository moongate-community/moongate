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
    public const string SpawnFunction = "on_spawn";
    public const string CreateFunction = "on_create";

    private readonly ILogger _logger = Log.ForContext<NpcService>();
    private readonly IMobileFactoryService _factory;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IWorldViewService _view;
    private readonly IDataAccess<MobileEntity> _mobileData;
    private readonly IDataAccess<ItemEntity> _itemData;
    private readonly IGameLoopService _loop;
    private readonly INpcScriptService? _scripts;
    private readonly IItemScriptService? _itemScripts;

    public NpcService(
        IMobileFactoryService factory,
        IMobileService mobiles,
        IItemService items,
        IWorldViewService view,
        IDataAccess<MobileEntity> mobileData,
        IDataAccess<ItemEntity> itemData,
        IGameLoopService loop,
        INpcScriptService? scripts = null,
        IItemScriptService? itemScripts = null
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
        _itemScripts = itemScripts;
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
        IReadOnlyDictionary<string, object?>? props = null,
        CancellationToken cancellationToken = default
    )
    {
        var spawned = await _factory.SpawnAsync(templateId, map, location, props, cancellationToken);
        var npc = spawned.Mobile;

        // Saved already: from here it is live whatever the caller does.
        await OnLoopAsync(
            () =>
            {
                // Queued first, so the new items' on_create and then on_spawn run before what entering the world queues,
                // such as on_mobile_in_range; they still run after this action, with the NPC in the world, dressed and
                // shown.
                var created = spawned.Equipment.Append(spawned.Backpack).Concat(spawned.BackpackItems).ToList();

                foreach (var item in created)
                {
                    _itemScripts?.Queue(item, CreateFunction);
                }

                _scripts?.Queue(npc, SpawnFunction);
                _mobiles.EnterWorld(npc);
                _items.Add(created);
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
