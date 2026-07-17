using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.UO.Data.Names;

namespace Moongate.Server.Services.Mobiles;

/// <summary>
/// In-memory registry of name pools, queryable by type. Populated at startup by
/// <see cref="Moongate.Server.Loaders.NamesLoader" />.
/// </summary>
public sealed class NameService : INameService
{
    private readonly Dictionary<string, NameList> _byType = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<NameList> All => [.. _byType.Values.OrderBy(list => list.Type)];

    public int Count => _byType.Count;

    public NameList? GetByType(string type)
        => _byType.GetValueOrDefault(type);

    public void Register(NameList list)
        => _byType[list.Type] = list;
}
