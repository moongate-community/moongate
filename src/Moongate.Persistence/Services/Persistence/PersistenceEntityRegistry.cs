using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;

namespace Moongate.Persistence.Services.Persistence;

/// <summary>
/// Default in-memory registry of persistence entity descriptors.
/// </summary>
public sealed class PersistenceEntityRegistry : IPersistenceEntityRegistry
{
    private readonly Dictionary<(Type EntityType, Type KeyType), IPersistenceEntityDescriptor> _descriptorsByClrTypes = [];
    private readonly Dictionary<ushort, IPersistenceEntityDescriptor> _descriptorsByTypeId = [];

    public bool IsFrozen { get; private set; }

    public void Freeze()
        => IsFrozen = true;

    public IPersistenceEntityDescriptor GetDescriptor(ushort typeId)
        => _descriptorsByTypeId.TryGetValue(typeId, out var descriptor)
               ? descriptor
               : throw new KeyNotFoundException($"No persistence descriptor registered for type id {typeId}.");

    public IPersistenceEntityDescriptor<TEntity, TKey> GetDescriptor<TEntity, TKey>()
        => _descriptorsByClrTypes.TryGetValue((typeof(TEntity), typeof(TKey)), out var descriptor)
               ? (IPersistenceEntityDescriptor<TEntity, TKey>)descriptor
               : throw new KeyNotFoundException(
                     $"No persistence descriptor registered for entity '{typeof(TEntity).FullName}' and key '{typeof(TKey).FullName}'."
                 );

    public IReadOnlyCollection<IPersistenceEntityDescriptor> GetRegisteredDescriptors()
        => _descriptorsByTypeId.Values.OrderBy(static descriptor => descriptor.TypeId).ToArray();

    public bool IsRegistered(ushort typeId)
        => _descriptorsByTypeId.ContainsKey(typeId);

    public bool IsRegistered<TEntity, TKey>()
        => _descriptorsByClrTypes.ContainsKey((typeof(TEntity), typeof(TKey)));

    public void Register<TEntity, TKey>(PersistenceEntityDescriptor<TEntity, TKey> descriptor)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (IsFrozen)
        {
            throw new InvalidOperationException("The persistence entity registry is frozen.");
        }

        if (_descriptorsByTypeId.ContainsKey(descriptor.TypeId))
        {
            throw new InvalidOperationException(
                $"A persistence descriptor is already registered for type id {descriptor.TypeId}."
            );
        }

        var typeKey = (descriptor.EntityType, descriptor.KeyType);

        if (_descriptorsByClrTypes.ContainsKey(typeKey))
        {
            throw new InvalidOperationException(
                $"A persistence descriptor is already registered for entity '{descriptor.EntityType.FullName}' and key '{descriptor.KeyType.FullName}'."
            );
        }

        _descriptorsByTypeId[descriptor.TypeId] = descriptor;
        _descriptorsByClrTypes[typeKey] = descriptor;
    }
}
