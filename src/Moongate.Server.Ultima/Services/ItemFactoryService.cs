using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Items;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Builds items from <see cref="IItemTemplateService" /> and writes them to the world database; the database gives
///     each new item a serial in the item range.
/// </summary>
public class ItemFactoryService : IItemFactoryService
{
    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;
    private readonly MoongatePersistenceService _persistence;

    public ItemFactoryService(IItemTemplateService templates, ITileDataService tiles, MoongatePersistenceService persistence)
    {
        _templates = templates;
        _tiles = tiles;
        _persistence = persistence;
    }

    public ItemEntity Create(string templateId, int? amount = null, Hue? hue = null)
    {
        var template = _templates.Get(templateId);
        var resolvedAmount = amount ?? template.Amount?.Resolve() ?? 1;

        if (resolvedAmount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), resolvedAmount, "An item amount must be at least 1.");
        }

        if (resolvedAmount > 1 && !template.EffectiveStackable(_tiles))
        {
            throw new ArgumentException(
                $"Item template '{templateId}' does not stack, so it cannot have an amount of {resolvedAmount}.",
                nameof(amount)
            );
        }

        return new ItemEntity
        {
            TemplateId = template.Id,
            ItemId = (int)template.ItemId.Value,
            Hue = hue ?? template.Hue.Resolve(),
            Amount = resolvedAmount,
            Rarity = template.Rarity.Resolve()
        };
    }

    public Task SaveAsync(ItemEntity item, CancellationToken cancellationToken = default)
    {
        return SaveAsync([item], cancellationToken);
    }

    public Task SaveAsync(IReadOnlyList<ItemEntity> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            EnsurePlaced(item);
        }

        return _persistence.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async transaction =>
            {
                foreach (var item in items)
                {
                    await SaveAsync(transaction, item, cancellationToken);
                }
            },
            cancellationToken
        );
    }

    public async Task SaveAsync(
        IPersistenceTransaction transaction,
        ItemEntity item,
        CancellationToken cancellationToken = default
    )
    {
        EnsurePlaced(item);
        await transaction.GetDataAccess<ItemEntity>().UpsertAsync(item, cancellationToken);

        if (!item.Id.IsItem)
        {
            throw new InvalidOperationException($"Item '{item.TemplateId}' was saved with {item.Id}, outside the item range.");
        }
    }

    private static void EnsurePlaced(ItemEntity item)
    {
        if (item.Location == ItemLocationType.None)
        {
            throw new InvalidOperationException(
                $"Item '{item.TemplateId}' has no location: put it on the ground, in a container or on a mobile before saving it."
            );
        }
    }
}
