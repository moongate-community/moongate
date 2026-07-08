using Moongate.Server.Interfaces;
using Moongate.UO.Data.Names;

namespace Moongate.Server.Services;

/// <summary>
/// In-memory registry of name pools, queryable by type. Populated at startup by
/// <see cref="Moongate.Server.Loaders.NamesLoader" />.
/// </summary>
public sealed class NameService : INameService
{
    private readonly Dictionary<string, NameList> _byType = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<NameList> All => [.. _byType.Values.OrderBy(list => list.Type)];

    public int Count => _byType.Count;

    public void Register(NameList list)
    {
        _byType[list.Type] = list;
    }

    public NameList? GetByType(string type)
    {
        return _byType.GetValueOrDefault(type);
    }
}
