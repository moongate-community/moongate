using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Builds items with a real <see cref="ItemFactoryService" /> and "saves" them without a database: an item without a
///     serial gets the next one from 0x40000001, and every created template ID and saved batch is recorded.
/// </summary>
public sealed class FakeItemFactoryService : IItemFactoryService
{
    private readonly ItemFactoryService _factory;
    private uint _next = 0x40000001;

    public List<IReadOnlyList<ItemEntity>> Saved { get; } = [];

    public List<string> CreatedTemplateIds { get; } = [];

    /// <summary>
    ///     The 1-based number of the save call that throws, as a failing database would; 0 never throws.
    /// </summary>
    public int FailingSave { get; set; }

    public FakeItemFactoryService(IItemTemplateService templates, ITileDataService tiles)
    {
        _factory = new(templates, tiles, null!);
    }

    public ItemEntity Create(string templateId, int? amount = null, Hue? hue = null)
    {
        var item = _factory.Create(templateId, amount, hue);
        CreatedTemplateIds.Add(templateId);

        return item;
    }

    public Task SaveAsync(ItemEntity item, CancellationToken cancellationToken = default)
    {
        return SaveAsync([item], cancellationToken);
    }

    public Task SaveAsync(IReadOnlyList<ItemEntity> items, CancellationToken cancellationToken = default)
    {
        if (FailingSave == Saved.Count + 1)
        {
            Saved.Add([]);

            throw new IOException("The database is gone.");
        }

        foreach (var item in items.Where(item => item.Id == default))
        {
            item.Id = new Serial(_next++);
        }

        Saved.Add(items.ToList());

        return Task.CompletedTask;
    }

    public Task SaveAsync(IPersistenceTransaction transaction, ItemEntity item, CancellationToken cancellationToken = default)
    {
        return SaveAsync([item], cancellationToken);
    }
}
