using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Interfaces;

/// <summary>
///     Declares one module's PostgreSQL schema, target and complete entity ownership.
///     Entity mappings are immutable per CLR type and must be declared with FreeSql attributes.
///     Application and plugin code must not reconfigure persistence entity types through another FreeSql instance.
/// </summary>
public interface IPersistenceModule
{
    /// <summary>
    ///     Gets the stable module identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    ///     Gets the lowercase snake_case PostgreSQL schema owned by the module.
    /// </summary>
    string Schema { get; }

    /// <summary>
    ///     Gets the database target that stores this module's entities.
    /// </summary>
    PersistenceDatabaseTarget DatabaseTarget { get; }

    /// <summary>
    ///     Gets the complete set of entity types owned by the module.
    ///     Each type must have one stable attribute mapping shared by every database owner in the process.
    /// </summary>
    IReadOnlyCollection<Type> EntityTypes { get; }
}
