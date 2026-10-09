using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     What can be tamed: see <see cref="ITamingService" />. Built on first use, after the loaders have run.
/// </summary>
public sealed class TamingService : ITamingService
{
    private readonly Lazy<FrozenDictionary<string, TamingCreature>> _creatures;

    public int Count => _creatures.Value.Count;

    public TamingService(IDataLoaderService data)
    {
        _creatures = new(() => data.GetEntities<TamingCreature>()
            .ToFrozenDictionary(creature => creature.Template, StringComparer.Ordinal)
        );
    }

    public bool TryGet(string templateId, [NotNullWhen(true)] out TamingCreature? creature)
    {
        return _creatures.Value.TryGetValue(templateId, out creature);
    }
}
