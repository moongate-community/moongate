using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Services.Items;

/// <summary>Default <see cref="IItemFactoryService" />: maps item templates into unpersisted entities.</summary>
public sealed class ItemFactoryService : IItemFactoryService
{
    private readonly IItemTemplateService _templates;
    private readonly Random _random;

    public ItemFactoryService(IItemTemplateService templates, Random random)
    {
        _templates = templates;
        _random = random;
    }

    public IReadOnlyList<ItemEntity> CreateByCategory(string category, int count = 1, int amount = 1, Hue? hue = null)
    {
        var matches = _templates.All
            .Where(template => string.Equals(
                    template.Category,
                    category,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();

        return CreateFromPool(matches, count, amount, hue);
    }

    public IReadOnlyList<ItemEntity> CreateByTag(string tag, int count = 1, int amount = 1, Hue? hue = null)
    {
        var matches = _templates.All
            .Where(template => template.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return CreateFromPool(matches, count, amount, hue);
    }

    public IReadOnlyList<ItemEntity> CreateFromTemplate(string templateId, int count = 1, int amount = 1, Hue? hue = null)
    {
        var template = _templates.GetById(templateId);

        return template is null ? [] : Repeat(count, () => Build(template, amount, hue));
    }

    private static ItemEntity Build(ItemTemplate template, int amount, Hue? hue)
        => new()
        {
            TemplateId = template.Id,
            ItemId = template.ItemId,
            Hue = hue ?? new Hue((ushort)template.Hue),
            Name = template.Name,
            ScriptId = template.ScriptId,
            Rarity = template.Rarity,
            Description = template.Description,
            Amount = Math.Max(1, amount),
            FlippableItemIds = template.FlippableItemIds is { Count: > 0 } ? [.. template.FlippableItemIds] : []
        };

    private IReadOnlyList<ItemEntity> CreateFromPool(IReadOnlyList<ItemTemplate> pool, int count, int amount, Hue? hue)
        => pool.Count == 0 ? [] : Repeat(count, () => Build(pool[_random.Next(pool.Count)], amount, hue));

    private static IReadOnlyList<ItemEntity> Repeat(int count, Func<ItemEntity> build)
    {
        var result = new List<ItemEntity>();

        for (var i = 0; i < Math.Max(1, count); i++)
        {
            result.Add(build());
        }

        return result;
    }
}
