using MemoryPack;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Interfaces.Persistence;

namespace Moongate.Persistence.Data.Persistence;

internal interface IInternalPersistenceEntityDescriptor
{
    void ApplyRemove(PersistenceStateStore stateStore, byte[] payload);

    void ApplyUpsert(PersistenceStateStore stateStore, byte[] payload);
    EntitySnapshotBucket? CaptureBucket(PersistenceStateStore stateStore);

    void LoadBucket(PersistenceStateStore stateStore, EntitySnapshotBucket bucket);
}

/// <summary>
/// Default descriptor implementation for a persisted entity kind.
/// </summary>
public sealed class PersistenceEntityDescriptor<TEntity, TKey>
    : IPersistenceEntityDescriptor<TEntity, TKey>,
      IInternalPersistenceEntityDescriptor
    where TKey : notnull
{
    private readonly Func<TEntity, TKey> _keySelector;
    private readonly Func<TKey, byte[]> _serializeKey;
    private readonly Func<byte[], TKey> _deserializeKey;

    public PersistenceEntityDescriptor(
        ushort typeId,
        string typeName,
        int schemaVersion,
        Func<TEntity, TKey> keySelector,
        Func<TKey, byte[]>? serializeKey = null,
        Func<byte[], TKey>? deserializeKey = null
    )
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(keySelector);

        TypeId = typeId;
        TypeName = typeName;
        SchemaVersion = schemaVersion;
        _keySelector = keySelector;
        _serializeKey = serializeKey ?? (static key => MemoryPackSerializer.Serialize(key));
        _deserializeKey = deserializeKey ?? (static payload => MemoryPackSerializer.Deserialize<TKey>(payload));
    }

    public ushort TypeId { get; }

    public string TypeName { get; }

    public int SchemaVersion { get; }

    public Type EntityType => typeof(TEntity);

    public Type KeyType => typeof(TKey);

    public TEntity Clone(TEntity entity)
        => DeserializeEntity(SerializeEntity(entity));

    public IReadOnlyList<TEntity> DeserializeBucket(byte[] payload)
        => MemoryPackSerializer.Deserialize<TEntity[]>(payload) ?? [];

    public TEntity DeserializeEntity(byte[] payload)
        => MemoryPackSerializer.Deserialize<TEntity>(payload)!;

    public TKey DeserializeKey(byte[] payload)
        => _deserializeKey(payload);

    public TKey GetKey(TEntity entity)
        => _keySelector(entity);

    public byte[] SerializeBucket(IReadOnlyCollection<TEntity> entities)
        => MemoryPackSerializer.Serialize(entities.ToArray());

    public byte[] SerializeEntity(TEntity entity)
        => MemoryPackSerializer.Serialize(entity);

    public byte[] SerializeKey(TKey key)
        => _serializeKey(key);

    void IInternalPersistenceEntityDescriptor.ApplyRemove(PersistenceStateStore stateStore, byte[] payload)
    {
        var key = DeserializeKey(payload);
        stateStore.GetBucket<TEntity, TKey>(TypeId).Remove(key);
    }

    void IInternalPersistenceEntityDescriptor.ApplyUpsert(PersistenceStateStore stateStore, byte[] payload)
    {
        var entity = DeserializeEntity(payload);
        stateStore.GetBucket<TEntity, TKey>(TypeId)[_keySelector(entity)] = entity;
    }

    EntitySnapshotBucket? IInternalPersistenceEntityDescriptor.CaptureBucket(PersistenceStateStore stateStore)
    {
        var entities = stateStore.GetBucket<TEntity, TKey>(TypeId).Values.ToArray();

        if (entities.Length == 0)
        {
            return null;
        }

        return new()
        {
            TypeId = TypeId,
            TypeName = TypeName,
            SchemaVersion = SchemaVersion,
            Payload = SerializeBucket(entities)
        };
    }

    void IInternalPersistenceEntityDescriptor.LoadBucket(PersistenceStateStore stateStore, EntitySnapshotBucket bucket)
    {
        var typedBucket = stateStore.GetBucket<TEntity, TKey>(TypeId);
        typedBucket.Clear();

        foreach (var entity in DeserializeBucket(bucket.Payload))
        {
            typedBucket[_keySelector(entity)] = entity;
        }
    }
}
