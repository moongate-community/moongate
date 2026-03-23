using Moongate.Persistence.Data.Persistence;

namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Registry of persisted entity descriptors used by snapshot and journal infrastructure.
/// </summary>
public interface IPersistenceEntityRegistry
{
    /// <summary>
    /// Gets whether the registry is frozen and no more registrations are allowed.
    /// </summary>
    bool IsFrozen { get; }

    /// <summary>
    /// Prevents further registrations.
    /// </summary>
    void Freeze();

    /// <summary>
    /// Gets a descriptor by type id.
    /// </summary>
    IPersistenceEntityDescriptor GetDescriptor(ushort typeId);

    /// <summary>
    /// Gets a typed descriptor by entity and key type.
    /// </summary>
    IPersistenceEntityDescriptor<TEntity, TKey> GetDescriptor<TEntity, TKey>();

    /// <summary>
    /// Returns all registered descriptors.
    /// </summary>
    IReadOnlyCollection<IPersistenceEntityDescriptor> GetRegisteredDescriptors();

    /// <summary>
    /// Returns true when a descriptor exists for the type id.
    /// </summary>
    bool IsRegistered(ushort typeId);

    /// <summary>
    /// Returns true when a descriptor exists for the entity and key type pair.
    /// </summary>
    bool IsRegistered<TEntity, TKey>();

    /// <summary>
    /// Registers a persisted entity descriptor.
    /// </summary>
    void Register<TEntity, TKey>(PersistenceEntityDescriptor<TEntity, TKey> descriptor)
        where TKey : notnull;
}
