using Moongate.Core.Random;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Applies the starting item sets as UOX3 does: the sets of the character's best skills, then the common sets, then
///     the sets of its race and gender. The first item worn on a layer wins; the others go in the backpack.
/// </summary>
public class StartingItemsService : IStartingItemsService
{
    private readonly IDataLoaderService _dataLoaderService;
    private readonly IItemFactoryService _factory;
    private readonly IItemTemplateService _templates;
    private readonly IContainerLayoutService _layout;
    private readonly ITileDataService _tiles;
    private readonly MoongatePersistenceService _persistence;
    private readonly StartingItemsConfig _config;

    public StartingItemsService(
        IDataLoaderService dataLoaderService,
        IItemFactoryService factory,
        IItemTemplateService templates,
        IContainerLayoutService layout,
        ITileDataService tiles,
        MoongatePersistenceService persistence,
        StartingItemsConfig config
    )
    {
        _dataLoaderService = dataLoaderService;
        _factory = factory;
        _templates = templates;
        _layout = layout;
        _tiles = tiles;
        _persistence = persistence;
        _config = config;
    }

    public Task StartAsync()
    {
        foreach (var (key, templateId) in new[]
                 {
                     ("backpack_template", _config.BackpackTemplate), ("gold_template", _config.GoldTemplate)
                 })
        {
            if (!_templates.TryGet(templateId, out _))
            {
                throw new InvalidDataException($"starting_items.{key} '{templateId}' is not an item template.");
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ItemEntity>> GiveAsync(
        StartingItemsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var entries = SelectSets(request).SelectMany(set => set.Items).ToList();
        var given = new List<ItemEntity>();

        await _persistence.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async transaction =>
            {
                var backpack = _factory.Create(_config.BackpackTemplate);
                backpack.Equip(request.MobileId, LayerType.Backpack);
                await _factory.SaveAsync(transaction, backpack, cancellationToken);
                given.Add(backpack);
                var usedLayers = new HashSet<LayerType> { LayerType.Backpack };

                foreach (var entry in entries)
                {
                    foreach (var item in CreateEntry(entry, request, backpack, usedLayers))
                    {
                        await _factory.SaveAsync(transaction, item, cancellationToken);
                        given.Add(item);
                    }
                }

                if (_config.Gold > 0)
                {
                    var gold = _factory.Create(_config.GoldTemplate, _config.Gold);
                    PutInBackpack(gold, backpack);
                    await _factory.SaveAsync(transaction, gold, cancellationToken);
                    given.Add(gold);
                }
            },
            cancellationToken
        );

        return given;
    }

    private List<StartingItemSet> SelectSets(StartingItemsRequest request)
    {
        var sets = _dataLoaderService.GetEntities<StartingItemSet>();
        var bestSkills = request.Skills
                                .Where(pair => pair.Value > 0)
                                .OrderByDescending(pair => pair.Value)
                                .ThenBy(pair => pair.Key)
                                .Take(_config.BestSkills)
                                .Select(pair => pair.Key);

        return bestSkills.SelectMany(skill => sets.Where(set => !set.Common && set.Skill == skill && MatchesBody(set, request)))
                         .Concat(sets.Where(set => set.Common))
                         .Concat(sets.Where(set => !set.Common && set.Skill is null && MatchesBody(set, request)))
                         .ToList();
    }

    private static bool MatchesBody(StartingItemSet set, StartingItemsRequest request)
    {
        return (set.Race is null || set.Race == request.Race) && (set.Gender is null || set.Gender == request.Gender);
    }

    // A stackable template gives one item with the amount; any other gives that many items. Each item is yielded
    // before the next is created, so the caller saves them in order.
    private IEnumerable<ItemEntity> CreateEntry(
        StartingItemEntry entry,
        StartingItemsRequest request,
        ItemEntity backpack,
        HashSet<LayerType> usedLayers
    )
    {
        var templateId = entry.Items[BuiltInRng.Next(entry.Items.Count)];
        var template = _templates.Get(templateId);
        var amount = entry.Amount?.Roll() ?? 1;
        var stacks = template.EffectiveStackable(_tiles);

        for (var i = 0; i < (stacks ? 1 : amount); i++)
        {
            var item = _factory.Create(templateId, stacks ? amount : 1, entry.Hue?.Resolve());
            ApplyLootType(item, template, entry.Newbie);

            if (entry.Equip && template.EffectiveLayer(_tiles) is { } layer && usedLayers.Add(layer))
            {
                item.Equip(request.MobileId, layer);
                ApplyBodyHue(item, layer, request);
            }
            else
            {
                PutInBackpack(item, backpack);
            }

            yield return item;
        }
    }

    private static void ApplyLootType(ItemEntity item, ItemTemplate template, bool? newbie)
    {
        var lootType = newbie == false ? LootType.Regular : LootType.Newbied;

        if (lootType != template.EffectiveLootType())
        {
            item.Props = new ItemProps { LootType = lootType };
        }
    }

    private static void ApplyBodyHue(ItemEntity item, LayerType layer, StartingItemsRequest request)
    {
        var hue = layer is LayerType.Shirt or LayerType.OuterTorso ? request.ShirtHue :
                  layer is LayerType.Pants or LayerType.OuterLegs ? request.PantsHue : default;

        if (!hue.IsNone)
        {
            item.Hue = hue;
        }
    }

    private void PutInBackpack(ItemEntity item, ItemEntity backpack)
    {
        var (x, y) = _layout.RandomGridPosition(backpack.ItemId);
        item.PutInContainer(backpack.Id, x, y);
    }
}
