using Moongate.Core.Geometry;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Makes an item on the ground with what its template puts inside: the gold in piles, then each loot table rolled
///     once. The item is saved before its contents, which need its serial; if their save fails the item never enters
///     the world and its row is queued for deletion.
/// </summary>
public sealed class ItemSpawnService : IItemSpawnService
{
    public const string CreateFunction = "on_create";

    private const int MaxPile = ushort.MaxValue;

    private readonly IItemFactoryService _factory;
    private readonly IItemTemplateService _templates;
    private readonly ILootService _loot;
    private readonly IContainerLayoutService _layout;
    private readonly IItemService _items;
    private readonly IWorldViewService _view;
    private readonly IGameLoopService _loop;
    private readonly ItemsConfig _config;
    private readonly IItemScriptService? _scripts;

    public ItemSpawnService(
        IItemFactoryService factory,
        IItemTemplateService templates,
        ILootService loot,
        IContainerLayoutService layout,
        IItemService items,
        IWorldViewService view,
        IGameLoopService loop,
        ItemsConfig config,
        IItemScriptService? scripts = null
    )
    {
        _factory = factory;
        _templates = templates;
        _loot = loot;
        _layout = layout;
        _items = items;
        _view = view;
        _loop = loop;
        _config = config;
        _scripts = scripts;
    }

    public async Task<ItemEntity> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        IReadOnlyDictionary<string, object?>? props = null,
        CancellationToken cancellationToken = default
    )
    {
        var template = _templates.Get(templateId);
        var item = _factory.Create(templateId);
        item.PlaceOnGround(map, location);

        foreach (var (key, value) in props ?? new Dictionary<string, object?>())
        {
            item.SetProp(key, value);
        }

        await _factory.SaveAsync(item, cancellationToken);
        var contents = new List<ItemEntity>();

        for (var gold = template.Gold?.Roll() ?? 0; gold > 0; gold -= MaxPile)
        {
            Pack(item, _factory.Create(_config.GoldTemplate, Math.Min(gold, MaxPile)), contents);
        }

        foreach (var lootId in template.Loot ?? [])
        {
            foreach (var rolled in _loot.Roll(lootId))
            {
                Pack(item, rolled, contents);
            }
        }

        if (contents.Count > 0)
        {
            try
            {
                await _factory.SaveAsync(contents, cancellationToken);
            }
            catch
            {
                // The item is saved and its contents are not: its row goes with the next world save, or an empty
                // chest would come back at the next start.
                await OnLoopAsync(() => _items.Absorb(item));

                throw;
            }
        }

        // Saved already: from here the item is live whatever the caller does.
        await OnLoopAsync(
            () =>
            {
                foreach (var created in contents.Prepend(item))
                {
                    _scripts?.Queue(created, CreateFunction);
                }

                _items.Add(contents.Prepend(item));
                _view.ItemAppeared(item);
            }
        );

        return item;
    }

    private async Task OnLoopAsync(Action action)
    {
        var work = new LoopActionWorkItem(action);
        await _loop.PostAsync(work, CancellationToken.None);
        await work.Completion;
    }

    private void Pack(ItemEntity container, ItemEntity item, List<ItemEntity> contents)
    {
        item.PutInContainer(container.Id, _layout.RandomGridPosition(container.ItemId), ContainerSlotUtils.FirstFree(contents));
        contents.Add(item);
    }
}
