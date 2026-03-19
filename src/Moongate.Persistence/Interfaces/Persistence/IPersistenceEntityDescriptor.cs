namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Describes a registered persisted entity type.
/// </summary>
public interface IPersistenceEntityDescriptor
{
    /// <summary>
    /// Stable numeric identifier for the persisted entity kind.
    /// </summary>
    ushort TypeId { get; }

    /// <summary>
    /// Stable diagnostic name for the persisted entity kind.
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Version of the persisted entity schema.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    /// CLR type of the entity.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// CLR type of the entity key.
    /// </summary>
    Type KeyType { get; }
}

/// <summary>
/// Strongly typed descriptor for a persisted entity kind.
/// </summary>
public interface IPersistenceEntityDescriptor<TEntity, TKey> : IPersistenceEntityDescriptor
{
    /// <summary>
    /// Gets the entity key.
    /// </summary>
    TKey GetKey(TEntity entity);

    /// <summary>
    /// Creates a detached entity clone suitable for external callers.
    /// </summary>
    TEntity Clone(TEntity entity);

    /// <summary>
    /// Serializes a single entity payload for journal upserts.
    /// </summary>
    byte[] SerializeEntity(TEntity entity);

    /// <summary>
    /// Deserializes a single entity payload from the journal.
    /// </summary>
    TEntity DeserializeEntity(byte[] payload);

    /// <summary>
    /// Serializes a key payload for journal removals.
    /// </summary>
    byte[] SerializeKey(TKey key);

    /// <summary>
    /// Deserializes a key payload from the journal.
    /// </summary>
    TKey DeserializeKey(byte[] payload);

    /// <summary>
    /// Serializes a snapshot bucket payload.
    /// </summary>
    byte[] SerializeBucket(IReadOnlyCollection<TEntity> entities);

    /// <summary>
    /// Deserializes a snapshot bucket payload.
    /// </summary>
    IReadOnlyList<TEntity> DeserializeBucket(byte[] payload);
}
