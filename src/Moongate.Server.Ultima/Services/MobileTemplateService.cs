using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Serves the templates <see cref="Loaders.MobileTemplatesLoader" /> loaded, the first time one is asked for.
/// </summary>
public class MobileTemplateService : IMobileTemplateService
{
    private readonly Lazy<FrozenDictionary<string, MobileTemplate>> _templates;

    public int Count => _templates.Value.Count;

    public MobileTemplateService(IDataLoaderService dataLoaderService)
    {
        _templates = new(
            () => dataLoaderService.GetEntities<MobileTemplate>()
                                   .ToFrozenDictionary(template => template.Id, StringComparer.Ordinal)
        );
    }

    public bool TryGet(string id, [NotNullWhen(true)] out MobileTemplate? template)
    {
        return _templates.Value.TryGetValue(id, out template);
    }

    public MobileTemplate Get(string id)
    {
        return TryGet(id, out var template)
            ? template
            : throw new KeyNotFoundException($"No mobile template has id '{id}'.");
    }
}
