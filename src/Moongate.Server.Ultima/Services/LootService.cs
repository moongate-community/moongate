using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Random;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Rolls the tables <see cref="Loaders.LootTemplatesLoader" /> loaded into items built by
///     <see cref="IItemFactoryService" />.
/// </summary>
public class LootService : ILootService
{
    // A guard only: the loader already rejects nested tables that loop.
    private const int MaxDepth = 8;

    private readonly Lazy<FrozenDictionary<string, LootTemplate>> _tables;
    private readonly IItemFactoryService _itemFactory;
    private readonly IItemTemplateService _itemTemplates;
    private readonly ITileDataService _tiles;

    public LootService(
        IDataLoaderService dataLoaderService,
        IItemFactoryService itemFactory,
        IItemTemplateService itemTemplates,
        ITileDataService tiles
    )
    {
        _tables = new(() =>
            dataLoaderService.GetEntities<LootTemplate>().ToFrozenDictionary(table => table.Id, StringComparer.Ordinal)
        );
        _itemFactory = itemFactory;
        _itemTemplates = itemTemplates;
        _tiles = tiles;
    }

    public bool TryGet(string id, [NotNullWhen(true)] out LootTemplate? table)
    {
        return _tables.Value.TryGetValue(id, out table);
    }

    public LootTemplate Get(string id)
    {
        return TryGet(id, out var table) ? table : throw new KeyNotFoundException($"No loot table has id '{id}'.");
    }

    public IReadOnlyList<ItemEntity> Roll(string lootTemplateId)
    {
        var items = new List<ItemEntity>();
        Roll(lootTemplateId, items, 0);

        return items;
    }

    private void Roll(string lootTemplateId, List<ItemEntity> items, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException($"Loot table '{lootTemplateId}' nests deeper than {MaxDepth} tables.");
        }

        var entries = Get(lootTemplateId).Entries;

        if (entries.Count == 0)
        {
            return;
        }

        var entry = Pick(entries);

        // LOOTLIST=table,n rolls the table n times: what the data plainly means (UOX3 itself spawns nothing for it).
        if (entry.LootTemplateId is { } nested)
        {
            for (var times = entry.Amount.Resolve(); times > 0; times--)
            {
                Roll(nested, items, depth + 1);
            }

            return;
        }

        if (entry.ItemId is not { } itemId)
        {
            return;
        }

        var amount = entry.Amount.Resolve();

        if (_itemTemplates.Get(itemId).EffectiveStackable(_tiles))
        {
            items.Add(_itemFactory.Create(itemId, amount));

            return;
        }

        for (var i = 0; i < amount; i++)
        {
            items.Add(_itemFactory.Create(itemId, 1));
        }
    }

    private static LootEntry Pick(List<LootEntry> entries)
    {
        var roll = BuiltInRng.Next(entries.Sum(entry => entry.Weight));

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
            {
                return entry;
            }

            roll -= entry.Weight;
        }

        return entries[^1];
    }
}
