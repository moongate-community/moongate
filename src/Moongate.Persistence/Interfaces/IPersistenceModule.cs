using FreeSql;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Interfaces;

/// <summary>Declares one module's PostgreSQL schema, target and complete entity ownership.</summary>
public interface IPersistenceModule
{
    /// <summary>Gets the stable module identifier.</summary>
    string Id { get; }

    /// <summary>Gets the lowercase snake_case PostgreSQL schema owned by the module.</summary>
    string Schema { get; }

    /// <summary>Gets the database target that stores this module's entities.</summary>
    PersistenceDatabaseTarget DatabaseTarget { get; }

    /// <summary>Gets the complete set of entity types owned by the module.</summary>
    IReadOnlyCollection<Type> EntityTypes { get; }

    /// <summary>Applies mappings for the module's owned entity types to the shared target ORM.</summary>
    /// <param name="orm">The target's shared FreeSql instance.</param>
    void Configure(IFreeSql orm);
}
