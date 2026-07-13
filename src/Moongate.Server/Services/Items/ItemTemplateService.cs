using Moongate.Server.Interfaces.Items;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Services.Items;

/// <summary>
/// In-memory registry of item templates, queryable by id. Populated at startup by
/// <see cref="Moongate.Server.Loaders.ItemTemplatesLoader" />.
/// </summary>
public sealed class ItemTemplateService : IItemTemplateService
{
    private readonly Dictionary<string, ItemTemplate> _byId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ItemTemplate> All => [.. _byId.Values];

    public int Count => _byId.Count;

    public void Register(ItemTemplate template)
    {
        _byId[template.Id] = template;
    }

    public ItemTemplate? GetById(string id)
    {
        return _byId.GetValueOrDefault(id);
    }
}
