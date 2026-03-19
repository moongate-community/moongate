using MessagePack;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Services.Persistence;

namespace Moongate.Persistence.Data.Persistence;

internal interface IInternalPersistenceEntityDescriptor
{
    EntitySnapshotBucket? CaptureBucket(PersistenceStateStore stateStore);

    void LoadBucket(PersistenceStateStore stateStore, EntitySnapshotBucket bucket);

    void ApplyUpsert(PersistenceStateStore stateStore, byte[] payload);

    void ApplyRemove(PersistenceStateStore stateStore, byte[] payload);
}

/// <summary>
/// Default descriptor implementation for a persisted entity kind.
/// </summary>
public sealed class PersistenceEntityDescriptor<TEntity, TKey, TSnapshot> :
    IPersistenceEntityDescriptor<TEntity, TKey>,
    IInternalPersistenceEntityDescriptor
    where TKey : notnull
{
    private readonly Func<TEntity, TKey> _keySelector;
    private readonly Func<TEntity, TSnapshot> _toSnapshot;
    private readonly Func<TSnapshot, TEntity> _fromSnapshot;
    private readonly Func<TKey, byte[]> _serializeKey;
    private readonly Func<byte[], TKey> _deserializeKey;

    public PersistenceEntityDescriptor(
        ushort typeId,
        string typeName,
        int schemaVersion,
        Func<TEntity, TKey> keySelector,
        Func<TEntity, TSnapshot> toSnapshot,
        Func<TSnapshot, TEntity> fromSnapshot,
        Func<TKey, byte[]>? serializeKey = null,
        Func<byte[], TKey>? deserializeKey = null
    )
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(toSnapshot);
        ArgumentNullException.ThrowIfNull(fromSnapshot);

        TypeId = typeId;
        TypeName = typeName;
        SchemaVersion = schemaVersion;
        _keySelector = keySelector;
        _toSnapshot = toSnapshot;
        _fromSnapshot = fromSnapshot;
        _serializeKey = serializeKey ?? (static key => MessagePackSerializer.Serialize(key));
        _deserializeKey = deserializeKey ?? (static payload => MessagePackSerializer.Deserialize<TKey>(payload));
    }

    public ushort TypeId { get; }

    public string TypeName { get; }

    public int SchemaVersion { get; }

    public Type EntityType => typeof(TEntity);

    public Type KeyType => typeof(TKey);

    public TKey GetKey(TEntity entity) => _keySelector(entity);

    public TEntity Clone(TEntity entity) => _fromSnapshot(_toSnapshot(entity));

    public byte[] SerializeEntity(TEntity entity) => MessagePackSerializer.Serialize(_toSnapshot(entity));

    public TEntity DeserializeEntity(byte[] payload)
        => _fromSnapshot(MessagePackSerializer.Deserialize<TSnapshot>(payload)!);

    public byte[] SerializeKey(TKey key) => _serializeKey(key);

    public TKey DeserializeKey(byte[] payload) => _deserializeKey(payload);

    public byte[] SerializeBucket(IReadOnlyCollection<TEntity> entities)
        => MessagePackSerializer.Serialize(entities.Select(_toSnapshot).ToArray());

    public IReadOnlyList<TEntity> DeserializeBucket(byte[] payload)
        => MessagePackSerializer.Deserialize<TSnapshot[]>(payload)!.Select(_fromSnapshot).ToArray();

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

    void IInternalPersistenceEntityDescriptor.ApplyUpsert(PersistenceStateStore stateStore, byte[] payload)
    {
        var entity = DeserializeEntity(payload);
        stateStore.GetBucket<TEntity, TKey>(TypeId)[_keySelector(entity)] = entity;
    }

    void IInternalPersistenceEntityDescriptor.ApplyRemove(PersistenceStateStore stateStore, byte[] payload)
    {
        var key = DeserializeKey(payload);
        stateStore.GetBucket<TEntity, TKey>(TypeId).Remove(key);
    }
}
