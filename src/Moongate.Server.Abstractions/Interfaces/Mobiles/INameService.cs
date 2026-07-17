using Moongate.UO.Data.Names;

namespace Moongate.Server.Abstractions.Interfaces.Mobiles;

/// <summary>In-memory registry of name pools, queryable by type.</summary>
public interface INameService
{
    /// <summary>All registered name lists, ordered by type.</summary>
    IReadOnlyList<NameList> All { get; }

    /// <summary>Number of registered name lists.</summary>
    int Count { get; }

    /// <summary>Returns the name list with the given type (case-insensitive), or null.</summary>
    NameList? GetByType(string type);

    /// <summary>Adds or replaces a name list, indexed by type.</summary>
    void Register(NameList list);
}
