using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The crafts and resource lists the loaders read, by id.
/// </summary>
public sealed class CraftService : ICraftService
{
    private readonly Lazy<Dictionary<string, CraftDefinition>> _crafts;
    private readonly Lazy<Dictionary<string, CraftResourceList>> _lists;

    public CraftService(IDataLoaderService data)
    {
        _crafts = new(() => data.GetEntities<CraftDefinition>().ToDictionary(craft => craft.Id, StringComparer.Ordinal));
        _lists = new(() => data.GetEntities<CraftResourceList>().ToDictionary(list => list.Id, StringComparer.Ordinal));
    }

    public CraftDefinition? Get(string id)
    {
        return id is not null && _crafts.Value.TryGetValue(id, out var craft) ? craft : null;
    }

    public IReadOnlyList<string>? Resource(string id)
    {
        return id is not null && _lists.Value.TryGetValue(id, out var list) ? list.Templates : null;
    }
}
