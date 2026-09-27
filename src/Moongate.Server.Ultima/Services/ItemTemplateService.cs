using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Serves the templates <see cref="Loaders.ItemTemplatesLoader" /> loaded, the first time one is asked for.
/// </summary>
public class ItemTemplateService : IItemTemplateService
{
    private readonly Lazy<FrozenDictionary<string, ItemTemplate>> _templates;

    public int Count => _templates.Value.Count;

    public ItemTemplateService(IDataLoaderService dataLoaderService)
    {
        _templates = new(
            () => dataLoaderService.GetEntities<ItemTemplate>()
                                   .ToFrozenDictionary(template => template.Id, StringComparer.Ordinal)
        );
    }

    public bool TryGet(string id, [NotNullWhen(true)] out ItemTemplate? template)
    {
        return _templates.Value.TryGetValue(id, out template);
    }

    public ItemTemplate Get(string id)
    {
        return TryGet(id, out var template)
            ? template
            : throw new KeyNotFoundException($"No item template has id '{id}'.");
    }
}
